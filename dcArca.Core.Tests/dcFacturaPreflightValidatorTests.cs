using dcArca.Core.Models;
using dcArca.Core.Services;
using Xunit;

namespace dcArca.Core.Tests;

public class dcFacturaPreflightValidatorTests
{
    [Fact]
    public void RequestValido_PasaSinNumeroAsignado()
    {
        var request = ValidRequest();
        request.NumeroComprobante = null;

        var result = dcFacturaPreflightValidator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("concepto")]
    [InlineData("fecha")]
    [InlineData("receptor")]
    [InlineData("totales")]
    [InlineData("servicio")]
    public void InvalidacionesDeterministicas_SeDetectanAntesDeIOManejo(string kind)
    {
        var request = ValidRequest();
        switch (kind)
        {
            case "concepto":
                request.Concepto = null;
                break;
            case "fecha":
                request.FechaComprobante = "2026-10-03";
                break;
            case "receptor":
                request.CuitReceptor = 123;
                break;
            case "totales":
                request.ImporteTotal = 999m;
                break;
            case "servicio":
                request.Concepto = dcConcepto.Servicios;
                request.FechaServicioDesde = null;
                request.FechaServicioHasta = null;
                request.FechaVencimiento = null;
                break;
        }

        var result = dcFacturaPreflightValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.Code));
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public void NotaSinAsociacion_FallaDeterministicamente()
    {
        var request = ValidRequest();
        request.TipoComprobante = dcTipoComprobante.NotaCreditoB;

        var result = dcFacturaPreflightValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Equal("NOTA_CBTEASOC_10197", result.Code);
    }

    private static dcFacturaRequest ValidRequest() => new()
    {
        TipoComprobante = dcTipoComprobante.FacturaB,
        Concepto = dcConcepto.Productos,
        CuitReceptor = 20123456786,
        TipoDocReceptor = (int)dcTipoDocumento.CUIT,
        CondicionIvaReceptor = dcCondicionIvaReceptor.ResponsableInscripto,
        ImporteNeto = 100m,
        ImporteIva = 21m,
        ImporteTotal = 121m,
        AlicuotaIva = dcAlicuotaIva.Veintiuno,
        MonedaId = "PES",
        MonedaCotizacion = 1m,
        FechaComprobante = "20261003"
    };
}
