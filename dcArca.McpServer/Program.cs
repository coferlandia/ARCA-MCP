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
if (builder.Environment.EnvironmentName == "Testing"
    && string.IsNullOrWhiteSpace(emissionIdempotencyDirectory))
{
    // WebApplicationFactory can start several independent test hosts in parallel. Give each
    // host its own durable filesystem topology unless a test explicitly supplies a store.
    emissionIdempotencyDirectory = Path.Combine(
        Path.GetTempPath(),
        "dcarca-mcp-host-tests",
        Guid.NewGuid().ToString("N"));
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
builder.Services.AddSingleton<IFiscalOperationIdentityProvider, SingleFiscalOperationIdentityProvider>();
builder.Services.AddSingleton<IAfipLogger>(sp =>
    new AfipLoggerAdapter(sp.GetRequiredService<ILoggerFactory>().CreateLogger("dcArca")));
builder.Services.AddSingleton<dcArcaAuthService>(sp => new dcArcaAuthService(
    arcaConfig.WsaaUrl, arcaConfig.CertificatePath, arcaConfig.CertificatePassword, arcaConfig.Cuit,
    logger: sp.GetRequiredService<IAfipLogger>()));
builder.Services.AddSingleton<IdcWsfeClient, dcWsfeClient>();
builder.Services.AddSingleton<IdcPadronClient, dcPadronClient>();
builder.Services.AddSingleton<IEmissionIdempotencyStore>(_ =>
    new FileSystemEmissionIdempotencyStore(emissionIdempotencyDirectory));
builder.Services.AddSingleton<IFiscalSeriesCoordinator>(sp =>
{
    var store = (FileSystemEmissionIdempotencyStore)sp.GetRequiredService<IEmissionIdempotencyStore>();
    return new FileSystemFiscalSeriesCoordinator(store.DirectoryPath);
});
builder.Services.AddSingleton<McpInvoiceSequencer>();
builder.Services.AddSingleton<IInvoiceIssuer>(sp => sp.GetRequiredService<McpInvoiceSequencer>());
builder.Services.AddSingleton<IPdfDocumentRenderer, PdfDocumentRenderer>();
builder.Services.AddSingleton<InvoicePdfService>();
builder.Services.AddSingleton<ExistingInvoicePdfService>();
builder.Services.AddSingleton<IApiKeyStore>(_ => new FileSystemApiKeyStore(apiKeysDirectory));
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
