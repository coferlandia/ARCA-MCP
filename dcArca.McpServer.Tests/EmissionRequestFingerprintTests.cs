using dcArca.Core.Models;
using Xunit;

namespace dcArca.McpServer.Tests;

public class EmissionRequestFingerprintTests
{
    [Fact]
    public void NumeroAsignadoNoAfectaFingerprint()
    {
        var a = Request();
        var b = Request();
        b.NumeroComprobante = 999;

        Assert.Equal(
            EmissionRequestFingerprint.RequestHash(a),
            EmissionRequestFingerprint.RequestHash(b));
    }

    [Fact]
    public void OrdenDeIvaYTributosNoAfectaFingerprint()
    {
        var a = Request();
        a.Iva =
        [
            new dcFacturaRequest.IvaDetalle { Alicuota = dcAlicuotaIva.Veintiuno, BaseImponible = 100m, Importe = 21m },
            new dcFacturaRequest.IvaDetalle { Alicuota = dcAlicuotaIva.Diez_Cinco, BaseImponible = 50m, Importe = 5.25m }
        ];
        a.Tributos =
        [
            new dcFacturaRequest.TributoDetalle { Id = 2, Descripcion = "Tasa B", BaseImponible = 10m, Alicuota = 1m, Importe = 0.1m },
            new dcFacturaRequest.TributoDetalle { Id = 1, Descripcion = "Tasa A", BaseImponible = 20m, Alicuota = 2m, Importe = 0.4m }
        ];

        var b = Request();
        b.Iva = a.Iva.AsEnumerable().Reverse().Select(x => new dcFacturaRequest.IvaDetalle
        {
            Alicuota = x.Alicuota,
            BaseImponible = x.BaseImponible,
            Importe = x.Importe
        }).ToList();
        b.Tributos = a.Tributos.AsEnumerable().Reverse().Select(x => new dcFacturaRequest.TributoDetalle
        {
            Id = x.Id,
            Descripcion = x.Descripcion,
            BaseImponible = x.BaseImponible,
            Alicuota = x.Alicuota,
            Importe = x.Importe
        }).ToList();

        Assert.Equal(
            EmissionRequestFingerprint.RequestHash(a),
            EmissionRequestFingerprint.RequestHash(b));
    }

    [Fact]
    public void CambioFiscalCambiaFingerprint()
    {
        var a = Request();
        var b = Request();
        b.ImporteTotal += 1m;

        Assert.NotEqual(
            EmissionRequestFingerprint.RequestHash(a),
            EmissionRequestFingerprint.RequestHash(b));
    }

    [Fact]
    public void KeyHashEsEstableYNoExponeLaKey()
    {
        var hash = EmissionRequestFingerprint.KeyHash("tenant:payment:123");

        Assert.Equal(64, hash.Length);
        Assert.DoesNotContain("tenant", hash, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(hash, EmissionRequestFingerprint.KeyHash("tenant:payment:123"));
    }

    private static dcFacturaRequest Request() => new()
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
        FechaComprobante = "20261001"
    };
}
