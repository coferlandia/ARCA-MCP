using dcArca.Core;
using dcArca.Core.Models;
using dcArca.Core.Services;
using dcArca.Core.Services.Logging;
using dcArca.McpServer;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var apiKeysDirectory = builder.Configuration["ApiKeys:Directory"];

// dcArcaConfig se carga con el mismo helper que usa dcArca.TestApp, valida CUIT/certificado al arrancar.
// appsettings.Development.json no trae su propia seccion dcArcaConfig (solo overrides de Logging), asi que
// solo "Testing" (que sí trae dcArcaConfig con el certificado placeholder) se resuelve a un archivo distinto.
var arcaSettingsFile = builder.Environment.EnvironmentName == "Testing"
    ? $"appsettings.{builder.Environment.EnvironmentName}.json"
    : "appsettings.json";
var arcaConfig = dcConfigurationHelper.LoadFromJson(
    Path.Combine(builder.Environment.ContentRootPath, arcaSettingsFile));

builder.Services.AddSingleton(arcaConfig);
builder.Services.AddSingleton<IAfipLogger>(sp =>
    new AfipLoggerAdapter(sp.GetRequiredService<ILoggerFactory>().CreateLogger("dcArca")));
builder.Services.AddSingleton<dcArcaAuthService>(sp => new dcArcaAuthService(
    arcaConfig.WsaaUrl, arcaConfig.CertificatePath, arcaConfig.CertificatePassword, arcaConfig.Cuit,
    logger: sp.GetRequiredService<IAfipLogger>()));
builder.Services.AddSingleton<IdcWsfeClient, dcWsfeClient>();
builder.Services.AddSingleton<IdcPadronClient, dcPadronClient>();
builder.Services.AddSingleton<McpInvoiceSequencer>();
builder.Services.AddSingleton<IApiKeyStore>(_ => new FileSystemApiKeyStore(apiKeysDirectory));

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
    // Habilita que [Authorize]/[AllowAnonymous] en los métodos de ArcaTools se
    // respeten por-tool (sin esto, MapMcp().RequireAuthorization() solo exige
    // "autenticado", cualquier scope vale para cualquier tool).
    .AddAuthorizationFilters();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));

app.MapMcp().RequireAuthorization();

app.Run();

public partial class Program { }
