using System.Net;
using dcArca.Core.Models;
using dcArca.Core.Services;
using Xunit;

namespace dcArca.Core.Tests;

public sealed class FiscalServiceIdentityTests
{
    [Fact]
    public void WsfeAuth_UsesRepresentedCuit_NotPrincipalCuit()
    {
        var config = Config();
        var xml = new dcWsfeSoapBuilder(config)
            .BuildUltimoComprobanteRequest("token", "sign", (int)dcTipoComprobante.FacturaC);

        Assert.Contains($"<ar:Cuit>{config.RepresentedCuit}</ar:Cuit>", xml, StringComparison.Ordinal);
        Assert.DoesNotContain($"<ar:Cuit>{config.PrincipalCuit}</ar:Cuit>", xml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PadronRequest_UsesPrincipalCuit_AndRequestedTarget()
    {
        const long targetCuit = 23333444559;
        const string cacheIdentity = "padron-identity-test";
        var tokenStore = new MemoryWsaaTokenStore();
        await tokenStore.WriteAsync(
            cacheIdentity + "_ws_sr_constancia_inscripcion",
            new WsaaTokenEntry("test-token", "test-sign", DateTime.UtcNow.AddHours(1)));

        var config = Config();
        var auth = new dcArcaAuthService(
            config.WsaaUrl,
            config.CertificatePath,
            config.CertificatePassword,
            cacheIdentity,
            serviceName: "ws_sr_constancia_inscripcion",
            tokenStore: tokenStore);
        var handler = new CapturingHandler(
            """
            <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/">
              <soapenv:Body>
                <getPersona_v2Response>
                  <personaReturn>
                    <datosGenerales>
                      <razonSocial>Persona de prueba</razonSocial>
                    </datosGenerales>
                  </personaReturn>
                </getPersona_v2Response>
              </soapenv:Body>
            </soapenv:Envelope>
            """);
        using var http = new HttpClient(handler);
        using var padron = new dcPadronClient(config, auth, http);

        var result = await padron.GetPersonaAsync(targetCuit);

        Assert.True(result.Success);
        Assert.NotNull(handler.RequestBody);
        Assert.Contains(
            $"<cuitRepresentada>{config.PrincipalCuit}</cuitRepresentada>",
            handler.RequestBody,
            StringComparison.Ordinal);
        Assert.Contains(
            $"<idPersona>{targetCuit}</idPersona>",
            handler.RequestBody,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            $"<cuitRepresentada>{config.RepresentedCuit}</cuitRepresentada>",
            handler.RequestBody,
            StringComparison.Ordinal);
    }

    private static dcArcaConfig Config()
        => new()
        {
            Environment = "homologacion",
            PrincipalCuit = "20123456789",
            RepresentedCuit = "30712345678",
            PuntoVenta = 7,
            CertificatePath = "unused-test.pfx",
            CertificatePassword = "",
            WsaaUrl = "https://example.invalid/wsaa",
            WsfeUrl = "https://example.invalid/wsfe",
            PadronUrl = "https://example.invalid/padron"
        };

    private sealed class CapturingHandler(string responseXml) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseXml)
            };
        }
    }
}
