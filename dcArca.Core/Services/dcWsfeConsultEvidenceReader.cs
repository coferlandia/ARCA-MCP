using System.Globalization;
using System.Xml;
using dcArca.Core.Models;

namespace dcArca.Core.Services;

internal static class dcWsfeConsultEvidenceReader
{
    public static dcConsultFiscalEvidence Read(XmlNode resultGetNode, XmlNamespaceManager ns)
    {
        ArgumentNullException.ThrowIfNull(resultGetNode);
        ArgumentNullException.ThrowIfNull(ns);

        var iva = new List<dcConsultIvaEvidence>();
        var ivaNodes = resultGetNode.SelectNodes("ar:Iva/ar:AlicIva", ns);
        if (ivaNodes is not null)
        {
            foreach (XmlNode item in ivaNodes)
            {
                iva.Add(new dcConsultIvaEvidence(
                    IntStatus(item, ns, "ar:Id"),
                    DecimalStatus(item, ns, "ar:BaseImp"),
                    DecimalStatus(item, ns, "ar:Importe")));
            }
        }

        var tributos = new List<dcConsultTributoEvidence>();
        var tributoNodes = resultGetNode.SelectNodes("ar:Tributos/ar:Tributo", ns);
        if (tributoNodes is not null)
        {
            foreach (XmlNode item in tributoNodes)
            {
                tributos.Add(new dcConsultTributoEvidence(
                    IntStatus(item, ns, "ar:Id"),
                    DecimalStatus(item, ns, "ar:BaseImp"),
                    DecimalStatus(item, ns, "ar:Alic"),
                    DecimalStatus(item, ns, "ar:Importe")));
            }
        }

        return new dcConsultFiscalEvidence(
            DecimalStatus(resultGetNode, ns, "ar:ImpTotal"),
            DecimalStatus(resultGetNode, ns, "ar:ImpNeto"),
            DecimalStatus(resultGetNode, ns, "ar:ImpIVA"),
            DecimalStatus(resultGetNode, ns, "ar:ImpTotConc"),
            DecimalStatus(resultGetNode, ns, "ar:ImpOpEx"),
            DecimalStatus(resultGetNode, ns, "ar:ImpTrib"),
            DecimalStatus(resultGetNode, ns, "ar:MonCotiz"),
            iva,
            tributos);
    }

    private static dcFiscalEvidenceStatus DecimalStatus(
        XmlNode parent,
        XmlNamespaceManager ns,
        string xpath)
    {
        var node = parent.SelectSingleNode(xpath, ns);
        if (node is null) return dcFiscalEvidenceStatus.Missing;
        return decimal.TryParse(node.InnerText, NumberStyles.Any, CultureInfo.InvariantCulture, out _)
            ? dcFiscalEvidenceStatus.Valid
            : dcFiscalEvidenceStatus.Invalid;
    }

    private static dcFiscalEvidenceStatus IntStatus(
        XmlNode parent,
        XmlNamespaceManager ns,
        string xpath)
    {
        var node = parent.SelectSingleNode(xpath, ns);
        if (node is null) return dcFiscalEvidenceStatus.Missing;
        return int.TryParse(node.InnerText, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            ? dcFiscalEvidenceStatus.Valid
            : dcFiscalEvidenceStatus.Invalid;
    }
}
