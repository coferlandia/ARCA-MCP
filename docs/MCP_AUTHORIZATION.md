# Autorización MCP por scopes

dcArca.McpServer trata los scopes de lectura y escritura como capacidades independientes.

- `arca:consultar`: habilita `consultar_ultimo_comprobante`, `consultar_comprobante`, `consultar_condiciones_iva` y `consultar_padron`.
- `arca:facturar`: habilita las operaciones de emisión.

`arca:facturar` no implica `arca:consultar`. Un cliente que necesite ambas capacidades debe solicitar ambos scopes, por ejemplo:

```
scope=arca:consultar arca:facturar
```

Cada API key guarda sus scopes en el store propio. El secreto se muestra una sola vez, solo se persiste su hash SHA-256 y una revocación se aplica al request siguiente.

Las claves se administran con `dcArca.Cli`:

```bash
ApiKeys__Directory=/data dotnet run --project dcArca.Cli -- \
  create-key --name secretaria --scope arca:consultar,arca:facturar
ApiKeys__Directory=/data dotnet run --project dcArca.Cli -- list-keys
ApiKeys__Directory=/data dotnet run --project dcArca.Cli -- revoke-key key_ID
```
