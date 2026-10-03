using dcArca.Core;
using dcArca.Core.Models;
using dcArca.Core.Services;
using dcArca.Core.Services.Logging;
using dcArca.McpServer;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;

if (await EmissionStoreMigrationCommand.TryRunAsync(args))
    return;

var builder = WebApplication.CreateBuilder(args);

var apiKeysDirectory = builder.Configuration["ApiKeys:Directory"];
var emissionIdempotencyDirectory = builder.Configuration["EmissionIdempotency:Directory"];
var fiscalContextsDirectory = builder.Configuration["FiscalContexts:Directory"];
if (builder.Environment.EnvironmentName == "Testing")
{
    var testHostRoot = Path.Combine(
        Path.GetTempPath(),
        "dcarca-mcp-host-tests",
        Guid.NewGuid().ToString("N"));
    // WebApplicationFactory can start several independent hosts in parallel. Unless a test
    // explicitly supplies persistence, isolate all fiscal topology owned by that host.
    emissionIdempotencyDirectory ??= Path.Combine(testHostRoot, "emission-idempotency");
    fiscalContextsDirectory ??= Path.Combine(testHostRoot, "fiscal-contexts");
}

var arcaSettingsFile = builder.Environment.EnvironmentName == "Testing"
    ? $"appsettings.{builder.Environment.EnvironmentName}.json"
    : "appsettings.json";
var arcaConfig = dcConfigurationHelper.LoadFromJson(
    Path.Combine(builder.Environment.ContentRootPath, arcaSettingsFile));
var fiscalContextOptions = SingleFiscalContextOptions.FromConfiguration(
    builder.Configuration.GetSection("FiscalContext"));

builder.Services.AddSingleton(arcaConfig);
builder.Services.AddSingleton(fiscalContextOptions);
builder.Services.AddSingleton<IAfipLogger>(sp =>
    new AfipLoggerAdapter(sp.GetRequiredService<ILoggerFactory>().CreateLogger("dcArca")));
builder.Services.AddSingleton(_ => new FileSystemEmissionIdempotencyStore(emissionIdempotencyDirectory));
builder.Services.AddSingleton<IEmissionIdempotencyStore>(sp =>
    sp.GetRequiredService<FileSystemEmissionIdempotencyStore>());
builder.Services.AddSingleton<IEmissionOperationInspector, FileSystemEmissionOperationInspector>();
builder.Services.AddSingleton<IFiscalSeriesCoordinator>(sp =>
{
    var store = sp.GetRequiredService<FileSystemEmissionIdempotencyStore>();
    return new FileSystemFiscalSeriesCoordinator(store.DirectoryPath);
});
builder.Services.AddSingleton<IRepresentedFiscalContextStore>(_ =>
    new FileSystemRepresentedFiscalContextStore(fiscalContextsDirectory));
builder.Services.AddSingleton<IFiscalCredentialMaterializer, FiscalCredentialMaterializer>();
builder.Services.AddSingleton<IFiscalContextRuntimeResolver, FiscalContextRuntimeResolver>();
builder.Services.AddSingleton<IFiscalAssignmentAuthorizationValidator, FiscalAssignmentAuthorizationValidator>();
builder.Services.AddSingleton<McpOperationContractService>();
builder.Services.AddSingleton<IPdfDocumentRenderer, PdfDocumentRenderer>();
builder.Services.AddSingleton<IApiKeyStore>(_ => new FileSystemApiKeyStore(apiKeysDirectory));
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient(nameof(FiscalAssignmentAuthorizationValidator), client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddHttpClient<IPdfClient, PdfClient>(client =>
{
    var baseUrl = builder.Configuration["Pdf:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(baseUrl)) client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});

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

// Acquire the supported V1 single-writer lease and rebuild/validate durable reservations
// before the server becomes ready to accept fiscal work.
var emissionStore = app.Services.GetRequiredService<IEmissionIdempotencyStore>();
var seriesCoordinator = app.Services.GetRequiredService<IFiscalSeriesCoordinator>();
await seriesCoordinator.InitializeAsync(emissionStore);

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));

app.MapMcp().RequireAuthorization();

app.Run();

public partial class Program { }
