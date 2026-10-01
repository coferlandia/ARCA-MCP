using dcArca.Core.Models;

namespace dcArca.McpServer;

public interface IInvoiceIssuer
{
    Task<dcFacturaResponse> EmitAsync(
        dcFacturaRequest factura,
        CancellationToken cancellationToken = default);
}
