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

// V2 derives CUIT/PV from authorized representations and credentials/endpoints
// from server-owned FiscalCredentials/FiscalEnvironments, never from dcArcaConfig.
var arcaConfig = new dcArcaConfig();
builder.Configuration.GetSection("dcArcaConfig").Bind(arcaConfig);
var fiscalContextOptions = new SingleFiscalContextOptions(
    "legacy-disabled", "legacy-disabled", "__v2_no_fallback__", 1, "no-legacy");

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
builder.Services.AddSingleton<IFiscalTechnicalContextStore>(_ =>
    new FileSystemFiscalTechnicalContextStore(fiscalContextsDirectory));
builder.Services.AddSingleton<IFiscalCredentialMaterializer, FiscalCredentialMaterializer>();
builder.Services.AddSingleton<IFiscalContextRuntimeResolver>(sp => new FiscalContextRuntimeResolver(
    sp.GetRequiredService<IFiscalTechnicalContextStore>(),
    sp.GetRequiredService<IEmissionIdempotencyStore>(),
    sp.GetRequiredService<SingleFiscalContextOptions>(),
    sp.GetRequiredService<IFiscalCredentialMaterializer>(),
    sp.GetRequiredService<IEmissionRecoveryGate>()));
builder.Services.AddSingleton<IFiscalAssignmentAuthorizationValidator>(sp => new FiscalAssignmentAuthorizationValidator(
    sp.GetRequiredService<IFiscalTechnicalContextStore>(),
    sp.GetRequiredService<IFiscalCredentialMaterializer>(),
    sp.GetRequiredService<IHttpClientFactory>()));
builder.Services.AddSingleton<FiscalRepresentationAdministration>();
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
    options.AddPolicy("ArcaAdministrar", policy => policy.RequireAssertion(context =>
        ArcaScopeAuthorization.HasScope(context.User, "arca:administrar")));
});

builder.Services.AddMcpServer()
    .WithTools<ArcaTools>()
    .WithTools<ArcaAdministrationTools>()
    .WithHttpTransport(options =>
    {
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    .AddAuthorizationFilters();

var app = builder.Build();

var recoveryGateService = app.Services.GetRequiredService<FileSystemEmissionRecoveryGate>();
using var recoveryRuntimeLease = recoveryGateService.AcquireRuntimeLease();

// The V2 fiscal catalog must be explicitly provisioned. Legacy identity data
// is not auto-transformed and requires a separately authorized cutover/reset.
await app.Services.GetRequiredService<IFiscalTechnicalContextStore>()
    .ListContextsAsync();

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
