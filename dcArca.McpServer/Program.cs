using dcArca.Core;
using dcArca.Core.Models;
using dcArca.Core.Services;
using dcArca.Core.Services.Logging;
using dcArca.McpServer;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;

if (await EmissionStoreMigrationCommand.TryRunAsync(args))
    return;
if (await EmissionStoreRecoveryCommand.TryRunAsync(args))
    return;

var builder = WebApplication.CreateBuilder(args);

var apiKeysDirectory = builder.Configuration["ApiKeys:Directory"];
var emissionIdempotencyDirectory = builder.Configuration["EmissionIdempotency:Directory"];
var fiscalContextsDirectory = builder.Configuration["FiscalContexts:Directory"];
var recoveryDirectory = builder.Configuration["Recovery:Directory"];
if (builder.Environment.EnvironmentName == "Testing")
{
    var testHostRoot = Path.Combine(
        Path.GetTempPath(),
        "dcarca-mcp-host-tests",
        Guid.NewGuid().ToString("N"));
    emissionIdempotencyDirectory ??= Path.Combine(testHostRoot, "emission-idempotency");
    fiscalContextsDirectory ??= Path.Combine(testHostRoot, "fiscal-contexts");
    recoveryDirectory ??= Path.Combine(testHostRoot, "recovery");
}

if (string.IsNullOrWhiteSpace(recoveryDirectory))
{
    var dataRoot = string.IsNullOrWhiteSpace(emissionIdempotencyDirectory)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dcArca")
        : Path.GetDirectoryName(Path.GetFullPath(emissionIdempotencyDirectory))
            ?? throw new InvalidOperationException("No se pudo resolver el directorio padre del store de emisiones.");
    recoveryDirectory = Path.Combine(dataRoot, "recovery");
}

var arcaConfig = dcConfigurationHelper.LoadFromConfiguration(builder.Configuration);
var fiscalContextOptions = SingleFiscalContextOptions.FromConfiguration(
    builder.Configuration.GetSection("FiscalContext"));

builder.Services.AddSingleton(arcaConfig);
builder.Services.AddSingleton(fiscalContextOptions);
builder.Services.AddSingleton<IAfipLogger>(sp =>
    new AfipLoggerAdapter(sp.GetRequiredService<ILoggerFactory>().CreateLogger("dcArca")));

builder.Services.AddSingleton(_ => new FileSystemEmissionIdempotencyStore(emissionIdempotencyDirectory));
builder.Services.AddSingleton<ObservableEmissionIdempotencyStore>();
builder.Services.AddSingleton<IEmissionIdempotencyStore>(sp =>
    sp.GetRequiredService<ObservableEmissionIdempotencyStore>());
builder.Services.AddSingleton<IEmissionOperationInspector, FileSystemEmissionOperationInspector>();

builder.Services.AddSingleton(sp =>
{
    var store = sp.GetRequiredService<FileSystemEmissionIdempotencyStore>();
    return new FileSystemFiscalSeriesCoordinator(store.DirectoryPath);
});
builder.Services.AddSingleton<ObservableFiscalSeriesCoordinator>();
builder.Services.AddSingleton<IFiscalSeriesCoordinator>(sp =>
    sp.GetRequiredService<ObservableFiscalSeriesCoordinator>());

builder.Services.AddSingleton(_ => new FileSystemEmissionRecoveryGate(recoveryDirectory));
builder.Services.AddSingleton<IEmissionRecoveryGate>(sp =>
    sp.GetRequiredService<FileSystemEmissionRecoveryGate>());
builder.Services.AddSingleton<IRepresentedFiscalContextStore>(_ =>
    new FileSystemRepresentedFiscalContextStore(fiscalContextsDirectory));
builder.Services.AddSingleton<IFiscalCredentialMaterializer, FiscalCredentialMaterializer>();
builder.Services.AddSingleton<IFiscalContextRuntimeResolver, FiscalContextRuntimeResolver>();
builder.Services.AddSingleton<IFiscalAssignmentAuthorizationValidator, FiscalAssignmentAuthorizationValidator>();
builder.Services.AddSingleton<McpOperationContractService>();

builder.Services.AddSingleton<PdfDocumentRenderer>();
builder.Services.AddSingleton<ObservablePdfDocumentRenderer>();
builder.Services.AddSingleton<IPdfDocumentRenderer>(sp =>
    sp.GetRequiredService<ObservablePdfDocumentRenderer>());

builder.Services.AddSingleton<IApiKeyStore>(_ => new FileSystemApiKeyStore(apiKeysDirectory));
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient(nameof(FiscalAssignmentAuthorizationValidator), client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});

static void ConfigurePdfHttpClient(HttpClient client, IConfiguration configuration)
{
    var baseUrl = configuration["Pdf:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(baseUrl)) client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
}

builder.Services.AddHttpClient<IPdfTemplateResolver, CreadorPdfTemplateResolver>(client =>
    ConfigurePdfHttpClient(client, builder.Configuration));
builder.Services.AddHttpClient<IPdfClient, PdfClient>(client =>
    ConfigurePdfHttpClient(client, builder.Configuration));

builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName, _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ArcaConsultar", policy => policy.RequireAssertion(context =>
        ArcaScopeAuthorization.HasScope(context.User, "arca:consultar")));
    options.AddPolicy("ArcaFacturar", policy => policy.RequireAssertion(context =>
        ArcaScopeAuthorization.HasScope(context.User, "arca:facturar")));
});

builder.Services.AddMcpServer()
    .WithTools<ArcaTools>()
    .WithHttpTransport(options =>
    {
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    .AddAuthorizationFilters();

var app = builder.Build();

var recoveryGateService = app.Services.GetRequiredService<FileSystemEmissionRecoveryGate>();
using var recoveryRuntimeLease = recoveryGateService.AcquireRuntimeLease();

var contextStore = app.Services.GetRequiredService<IRepresentedFiscalContextStore>();
if (!long.TryParse(arcaConfig.Cuit, out var legacyCuit) || legacyCuit <= 0)
    throw new InvalidOperationException("dcArcaConfig:Cuit debe ser numérico para inicializar el contexto fiscal legacy.");
var now = DateTimeOffset.UtcNow;
await contextStore.InitializeLegacyAsync(new RepresentedFiscalContextRecord(
    fiscalContextOptions.ContextId,
    fiscalContextOptions.Environment,
    legacyCuit,
    arcaConfig.PuntoVenta,
    FiscalContextOperationalState.Active,
    fiscalContextOptions.ContextRevision,
    LegacyDefault: true,
    Assignments:
    [
        new CredentialAssignmentRecord(
            fiscalContextOptions.CredentialAssignmentRevision,
            "legacy-default",
            CredentialAssignmentStatus.Active,
            "legacy-config-bootstrap",
            now,
            now,
            now,
            "legacy-bootstrap")
    ]));

var concreteEmissionStore = app.Services.GetRequiredService<FileSystemEmissionIdempotencyStore>();
var concreteSeriesCoordinator = app.Services.GetRequiredService<FileSystemFiscalSeriesCoordinator>();
await concreteSeriesCoordinator.InitializeAsync(concreteEmissionStore);

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));
app.MapGet("/health/ready", async (IEmissionRecoveryGate recoveryGate, CancellationToken cancellationToken) =>
{
    try
    {
        var block = await recoveryGate.GetBlockAsync(cancellationToken);
        return block is null
            ? Results.Ok(new { status = "ready" })
            : Results.Json(new
            {
                status = "not-ready",
                code = "RESTORE_RECONCILIATION_REQUIRED",
                restoreId = block.RestoreId,
                backupCreatedAt = block.BackupCreatedAt,
                restoredAt = block.RestoredAt
            }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception)
    {
        return Results.Json(new
        {
            status = "not-ready",
            code = "RECOVERY_GATE_UNREADABLE"
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapMcp().RequireAuthorization();

app.Run();

public partial class Program { }
