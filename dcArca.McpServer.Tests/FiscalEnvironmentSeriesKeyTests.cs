using dcArca.Core.Models;
using Xunit;

namespace dcArca.McpServer.Tests;

public sealed class FiscalEnvironmentSeriesKeyTests
{
    [Fact]
    public void DosContextosMismoAmbienteFiscal_CompartenSeriesKey()
    {
        var a = new FiscalOperationIdentity(
            "consumer-a", "ctx-a", "homologacion",
            20123456786, 7, (int)dcTipoComprobante.FacturaB, 1, "rev-a");
        var b = new FiscalOperationIdentity(
            "consumer-b", "ctx-b", "homologacion",
            20123456786, 7, (int)dcTipoComprobante.FacturaB, 9, "rev-b");

        Assert.Equal(a.SeriesKey, b.SeriesKey);
        Assert.Equal("homologacion:20123456786:7:6", a.SeriesKey);
    }

    [Fact]
    public void EnvironmentNoCanonicoEnIdentidadDurable_FallaCerrado()
    {
        var identity = new FiscalOperationIdentity(
            "consumer-a", "ctx-a", "HOMOLOGACION",
            20123456786, 7, (int)dcTipoComprobante.FacturaB, 1, "rev-a");

        Assert.Throws<InvalidDataException>(() => _ = identity.SeriesKey);
    }
}
