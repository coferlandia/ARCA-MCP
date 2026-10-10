/*
 * Copyright (c) 2025 Diego Cofré, DC Sistemas
 * www.diegocofre.com.ar
 *
 * Licensed under the Apache License, Version 2.0.
 */

using System.Text.Json;
using System.Security.Cryptography.X509Certificates;
using dcArca.Core.Models;
using dcArca.Core.Services;
using Microsoft.Extensions.Configuration;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

try
{
    if (args[0] == "diagnose-v2")
        return await DiagnoseV2Async(args, jsonOptions);

    if (args[0] is "create-key" or "list-keys" or "revoke-key" or "set-key-grants")
        return await ManageApiKeysAsync(args, jsonOptions);

    if (args[0] is "list-contexts" or "add-context" or "add-assignment" or "validate-assignment"
        or "activate-assignment" or "disable-assignment" or "set-context-state"
        or "list-representations" or "register-representation" or "select-representation-pv"
        or "list-points-of-sale" or "activate-representation" or "revoke-representation")
        return await ManageFiscalContextsAsync(args, jsonOptions);

    var config = LoadArcaConfig(LoadConfiguration());
    IdcWsfeClient wsfe = new dcWsfeClient(config);
    IdcPadronClient padron = new dcPadronClient(config);
    object result = args[0] switch
    {
        "facturar" => await FacturarAsync(args, wsfe),
        "ultimo-autorizado" => await wsfe.FECompUltimoAutorizadoAsync((dcTipoComprobante)int.Parse(args[1])),
        "consultar" => await wsfe.FECompConsultarAsync(long.Parse(args[1]), (dcTipoComprobante)int.Parse(args[2])),
        "padron" => await padron.GetPersonaAsync(long.Parse(args[1])),
        "condiciones-iva" => await wsfe.GetCondicionesIVAReceptorAsync(int.Parse(args[1]), long.Parse(args[2]), (dcTipoComprobante)int.Parse(args[3])),
        _ => throw new ArgumentException($"Comando desconocido: {args[0]}")
    };

    Console.WriteLine(JsonSerializer.Serialize(result, jsonOptions));
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    PrintUsage();
    return 1;
}

static IConfigurationRoot LoadConfiguration()
    => new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true)
        .AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"), optional: true)
        .AddEnvironmentVariables()
        .Build();

static dcArcaConfig LoadArcaConfig(IConfiguration configuration)
{
    var config = new dcArcaConfig();
    configuration.GetSection("dcArcaConfig").Bind(config);
    return config;
}

static async Task<dcFacturaResponse> FacturarAsync(string[] args, IdcWsfeClient wsfe)
{
    var json = args.Length > 1 && File.Exists(args[1])
        ? await File.ReadAllTextAsync(args[1])
        : await Console.In.ReadToEndAsync();

    var factura = JsonSerializer.Deserialize<dcFacturaRequest>(
        json,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new ArgumentException("JSON de factura inválido o vacío.");

    return await wsfe.FECAESolicitarAsync(factura);
}

static async Task<int> ManageApiKeysAsync(string[] args, JsonSerializerOptions jsonOptions)
{
    var configuration = LoadConfiguration();
    var store = new FileSystemApiKeyStore(configuration["ApiKeys:Directory"]);

    switch (args[0])
    {
        case "create-key":
        {
            var name = RequiredOption(args, "--name");
            var scopes = ParseCsv(RequiredOption(args, "--scope"));
            var consumer = Option(args, "--consumer")?.Trim();
            var grants = ParseGrants(Option(args, "--grant"));

            (ApiKeyRecord Record, string RawKey) created;
            if (string.IsNullOrWhiteSpace(consumer))
            {
                if (grants.Length > 0)
                    throw new ArgumentException("--grant requiere --consumer para evitar grants sin identidad estable.");
                created = await store.CreateAsync(name, scopes);
            }
            else
            {
                if (grants.Length == 0)
                    throw new ArgumentException("Una key con --consumer debe declarar al menos un --grant.");
                created = await store.CreateForConsumerAsync(name, consumer, scopes, grants);
            }

            Console.WriteLine(JsonSerializer.Serialize(new
            {
                created.Record.Id,
                created.Record.Name,
                created.Record.ConsumerId,
                created.Record.ContextGrants,
                created.Record.Scopes,
                created.Record.Active,
                created.Record.CreatedAt,
                Secret = created.RawKey,
            }, jsonOptions));
            return 0;
        }
        case "set-key-grants":
        {
            if (args.Length < 2) throw new ArgumentException("Falta el id de la API key.");
            var consumer = RequiredOption(args, "--consumer");
            var grants = ParseGrants(RequiredOption(args, "--grant"));
            if (grants.Length == 0) throw new ArgumentException("Debe indicarse al menos un grant.");
            if (!await store.SetConsumerAndGrantsAsync(args[1], consumer, grants))
                throw new ArgumentException("La API key no existe.");
            Console.WriteLine(JsonSerializer.Serialize(new { Updated = args[1], ConsumerId = consumer, Grants = grants }, jsonOptions));
            return 0;
        }
        case "list-keys":
        {
            var records = await store.ListAsync();
            Console.WriteLine(JsonSerializer.Serialize(records.Select(record => new
            {
                record.Id,
                record.Name,
                record.ConsumerId,
                record.ContextGrants,
                record.Scopes,
                record.Active,
                record.CreatedAt,
                record.RevokedAt,
                record.LastUsedAt,
            }), jsonOptions));
            return 0;
        }
        case "revoke-key":
            if (args.Length < 2) throw new ArgumentException("Falta el id de la API key.");
            if (!await store.RevokeAsync(args[1])) throw new ArgumentException("La API key no existe o ya fue revocada.");
            Console.WriteLine(JsonSerializer.Serialize(new { Revoked = args[1] }, jsonOptions));
            return 0;
        default:
            throw new ArgumentException($"Comando desconocido: {args[0]}");
    }
}

static async Task<int> ManageFiscalContextsAsync(string[] args, JsonSerializerOptions jsonOptions)
{
    var configuration = LoadConfiguration();
    var store = new FileSystemFiscalTechnicalContextStore(configuration["FiscalContexts:Directory"]);

    switch (args[0])
    {
        case "list-contexts":
        {
            var contexts = await store.ListContextsAsync();
            Console.WriteLine(JsonSerializer.Serialize(contexts, jsonOptions));
            return 0;
        }
        case "add-context":
        {
            var context = new FiscalTechnicalContextRecord(
                RequiredOption(args, "--id"),
                RequiredOption(args, "--environment"),
                FiscalContextOperationalState.Disabled,
                ParseInt(Option(args, "--context-revision") ?? "1", "--context-revision"),
                Array.Empty<CredentialAssignmentRecord>());
            await store.AddContextAsync(context);
            Console.WriteLine(JsonSerializer.Serialize(context, jsonOptions));
            return 0;
        }
        case "add-assignment":
        {
            await store.AddCandidateAssignmentAsync(
                RequiredOption(args, "--context"),
                RequiredOption(args, "--revision"),
                RequiredOption(args, "--credential"),
                RequiredOption(args, "--actor"));
            Console.WriteLine(JsonSerializer.Serialize(new { Added = RequiredOption(args, "--revision"), Status = "Candidate" }, jsonOptions));
            return 0;
        }
        case "validate-assignment":
        {
            var contextId = RequiredOption(args, "--context");
            var revision = RequiredOption(args, "--revision");
            var actor = RequiredOption(args, "--actor");
            var context = await store.GetContextAsync(contextId)
                ?? throw new InvalidOperationException("FISCAL_CONTEXT_NOT_FOUND");
            var assignment = context.Assignments.SingleOrDefault(x => x.AssignmentRevision == revision)
                ?? throw new InvalidOperationException("ASSIGNMENT_NOT_FOUND");
            var projected = new RepresentedFiscalContextRecord(context.ContextId, context.Environment,
                1, 1, context.OperationalState, context.ContextRevision, false, context.Assignments);
            var binding = HostBinding(configuration, projected, assignment);
            try
            {
                using var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                    binding.Config.CertificatePath, binding.Config.CertificatePassword);
                var now = DateTime.UtcNow;
                if (certificate.NotAfter.ToUniversalTime() <= now || certificate.NotBefore.ToUniversalTime() > now)
                    throw new InvalidOperationException("CERTIFICATE_NOT_VALID");
                var auth = CreateWsfeAuth(binding);
                await auth.GetTokenAsync();
                var evidence = $"WSAA|context={contextId}|rev={revision}|checked={DateTimeOffset.UtcNow:O}";
                await store.MarkAssignmentValidatedAsync(contextId, revision, evidence, actor);
                Console.WriteLine(JsonSerializer.Serialize(new { ContextId = contextId, Revision = revision,
                    Verified = true, Evidence = evidence }, jsonOptions));
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("TECHNICAL_CREDENTIAL_NOT_VERIFIED: " + ex.GetType().Name);
                return 2;
            }
        }
        case "activate-assignment":
        {
            var contextId = RequiredOption(args, "--context");
            var revision = RequiredOption(args, "--revision");
            var actor = RequiredOption(args, "--actor");
            await store.ActivateAssignmentAsync(contextId, revision, actor);
            Console.WriteLine(JsonSerializer.Serialize(new { ContextId = contextId, ActiveAssignment = revision }, jsonOptions));
            return 0;
        }
        case "disable-assignment":
        {
            var contextId = RequiredOption(args, "--context");
            var revision = RequiredOption(args, "--revision");
            var actor = RequiredOption(args, "--actor");
            await store.DisableAssignmentAsync(contextId, revision, actor);
            Console.WriteLine(JsonSerializer.Serialize(new { ContextId = contextId, DisabledAssignment = revision }, jsonOptions));
            return 0;
        }
        case "set-context-state":
        {
            var contextId = RequiredOption(args, "--context");
            var stateText = RequiredOption(args, "--state");
            var actor = RequiredOption(args, "--actor");
            if (!Enum.TryParse<FiscalContextOperationalState>(stateText, ignoreCase: true, out var state))
                throw new ArgumentException("--state debe ser Active, ReadOnly o Disabled.");
            await store.SetContextStateAsync(contextId, state, actor);
            Console.WriteLine(JsonSerializer.Serialize(new { ContextId = contextId, State = state }, jsonOptions));
            return 0;
        }
        case "list-representations":
        {
            var records = await store.ListRepresentationsAsync(RequiredOption(args, "--context"));
            Console.WriteLine(JsonSerializer.Serialize(records, jsonOptions));
            return 0;
        }
        case "register-representation":
        {
            await store.RegisterCandidateAsync(RequiredOption(args, "--context"),
                RequiredOption(args, "--consumer"), ParseLong(RequiredOption(args, "--cuit"), "--cuit"),
                RequiredOption(args, "--actor"));
            Console.WriteLine(JsonSerializer.Serialize(new { Status = "Pending" }, jsonOptions));
            return 0;
        }
        case "select-representation-pv":
        {
            await store.SelectPointOfSaleAsync(RequiredOption(args, "--context"),
                RequiredOption(args, "--consumer"), ParseLong(RequiredOption(args, "--cuit"), "--cuit"),
                ParseInt(RequiredOption(args, "--point-of-sale"), "--point-of-sale"),
                RequiredOption(args, "--actor"));
            Console.WriteLine(JsonSerializer.Serialize(new { Status = "Pending", PvSelected = true }, jsonOptions));
            return 0;
        }
        case "list-points-of-sale":
        case "activate-representation":
        {
            var contextId = RequiredOption(args, "--context");
            var consumer = RequiredOption(args, "--consumer");
            var cuit = ParseLong(RequiredOption(args, "--cuit"), "--cuit");
            var context = await store.GetContextAsync(contextId)
                ?? throw new InvalidOperationException("FISCAL_CONTEXT_NOT_FOUND");
            var assignment = context.ActiveAssignment
                ?? throw new InvalidOperationException("ACTIVE_ASSIGNMENT_REQUIRED");
            var representations = await store.ListRepresentationsAsync(contextId);
            if (!representations.Any(x => x.ConsumerId == consumer && x.RepresentedCuit == cuit
                && x.Status != FiscalRepresentationStatus.Revoked))
                throw new InvalidOperationException("FISCAL_REPRESENTATION_CANDIDATE_REQUIRED");
            var pv = args[0] == "activate-representation"
                ? ParseInt(RequiredOption(args, "--point-of-sale"), "--point-of-sale") : 1;
            var projected = new RepresentedFiscalContextRecord(contextId, context.Environment, cuit,
                pv, context.OperationalState, context.ContextRevision, false, context.Assignments);
            var binding = HostBinding(configuration, projected, assignment);
            var probe = new dcWsfePointOfSaleProbe();
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var auth = CreateWsfeAuth(binding);
            if (args[0] == "list-points-of-sale")
            {
                var result = await probe.ListAsync(binding.Config, auth, httpClient);
                Console.WriteLine(JsonSerializer.Serialize(result, jsonOptions));
                return result.Verified ? 0 : 2;
            }
            var candidate = await store.GetRepresentationAsync(contextId, consumer, cuit, pv)
                ?? throw new InvalidOperationException("FISCAL_REPRESENTATION_NOT_FOUND");
            if (candidate.Status is not (FiscalRepresentationStatus.Pending or FiscalRepresentationStatus.Verified or FiscalRepresentationStatus.Active or FiscalRepresentationStatus.ActionRequired))
                throw new InvalidOperationException("FISCAL_REPRESENTATION_STATE_INVALID");
            var resultProbe = await probe.ProbeAsync(binding.Config, auth, httpClient);
            if (!resultProbe.Verified)
            {
                Console.WriteLine(JsonSerializer.Serialize(resultProbe, jsonOptions));
                return 2;
            }
            var actor = RequiredOption(args, "--actor");
            var evidence = $"FEParamGetPtosVenta|context={contextId}|env={context.Environment}|cuit={cuit}|pv={pv}|rev={assignment.AssignmentRevision}|checked={resultProbe.CheckedAt:O}";
            await store.MarkRepresentationVerifiedAsync(contextId, consumer, cuit, pv, evidence, actor);
            await store.ActivateRepresentationAsync(contextId, consumer, cuit, pv, actor);
            Console.WriteLine(JsonSerializer.Serialize(new { Status = "Active", ContextId = contextId,
                ConsumerId = consumer, Cuit = cuit, PointOfSale = pv }, jsonOptions));
            return 0;
        }
        case "revoke-representation":
        {
            await store.RevokeRepresentationAsync(RequiredOption(args, "--context"),
                RequiredOption(args, "--consumer"), ParseLong(RequiredOption(args, "--cuit"), "--cuit"),
                ParseInt(RequiredOption(args, "--point-of-sale"), "--point-of-sale"),
                RequiredOption(args, "--actor"));
            Console.WriteLine(JsonSerializer.Serialize(new { Status = "Revoked" }, jsonOptions));
            return 0;
        }
        default:
            throw new ArgumentException($"Comando desconocido: {args[0]}");
    }
}

// Consultas no emisoras con la misma credencial y ambiente del contexto técnico V2.
// El resultado omite respuestas SOAP, token/sign y datos personales del Padrón.
static async Task<int> DiagnoseV2Async(string[] args, JsonSerializerOptions jsonOptions)
{
    var contextId = RequiredOption(args, "--context");
    var cuit = ParseLong(RequiredOption(args, "--cuit"), "--cuit");
    var pv = ParseInt(RequiredOption(args, "--point-of-sale"), "--point-of-sale");
    var tipo = ParseInt(RequiredOption(args, "--tipo-comprobante"), "--tipo-comprobante");
    var store = new FileSystemFiscalTechnicalContextStore(LoadConfiguration()["FiscalContexts:Directory"]);
    var context = await store.GetContextAsync(contextId)
        ?? throw new InvalidOperationException("FISCAL_CONTEXT_NOT_FOUND");
    var assignment = context.ActiveAssignment
        ?? throw new InvalidOperationException("ACTIVE_ASSIGNMENT_REQUIRED");
    var projected = new RepresentedFiscalContextRecord(contextId, context.Environment,
        cuit, pv, context.OperationalState, context.ContextRevision, false, context.Assignments);
    var binding = HostBinding(LoadConfiguration(), projected, assignment);

    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    var wsfeAuth = CreateWsfeAuth(binding);
    var listing = await new dcWsfePointOfSaleProbe().ListAsync(binding.Config, wsfeAuth, http);

    using var wsfe = new dcWsfeClient(binding.Config, wsfeAuth, http);
    var ultimo = await wsfe.FECompUltimoAutorizadoAsync((dcTipoComprobante)tipo);

    var padronAuth = new dcArcaAuthService(binding.Config.WsaaUrl,
        binding.Config.CertificatePath, binding.Config.CertificatePassword,
        binding.CacheIdentity, serviceName: "ws_sr_constancia_inscripcion");
    using var padron = new dcPadronClient(binding.Config, padronAuth, http);
    var persona = await padron.GetPersonaAsync(cuit);

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        ContextId = contextId,
        Environment = context.Environment,
        WsfePoints = new { listing.Verified, listing.Code, listing.SafeMessage,
            PointOfSaleFound = listing.Points.Any(x => x.Number == pv) },
        WsfeLastAuthorized = new { ultimo.Success, ultimo.Codigo,
            ultimo.NumeroComprobante },
        Padron = new { persona.Success, persona.ErrorCodigo,
            SafeMessage = SafeDiagnosticText(persona.ErrorDescripcion?.Split(" | Detalle:", 2)[0]) },
    }, jsonOptions));
    return listing.Verified && ultimo.Success && persona.Success ? 0 : 2;
}

static string SafeDiagnosticText(string? message)
{
    if (string.IsNullOrWhiteSpace(message)) return string.Empty;
    var safe = System.Text.RegularExpressions.Regex.Replace(message,
        @"[^\p{L}\p{N} .,;:()_\-/]", " ");
    safe = System.Text.RegularExpressions.Regex.Replace(safe, @"\d{8,}", "[id]");
    safe = System.Text.RegularExpressions.Regex.Replace(safe, @"\S{48,}", "[redacted]");
    return safe.Length <= 180 ? safe.Trim() : safe[..180].Trim();
}

static FiscalCredentialHostBinding HostBinding(
    IConfiguration configuration, RepresentedFiscalContextRecord context, CredentialAssignmentRecord assignment)
{
    var legacyConfig = LoadArcaConfig(configuration);
    var legacyEnvironment = configuration["FiscalContext:Environment"] ?? context.Environment;
    return new FiscalCredentialHostBindingResolver(configuration, legacyConfig, legacyEnvironment)
        .Resolve(context, assignment);
}

static dcArcaAuthService CreateWsfeAuth(FiscalCredentialHostBinding binding)
    => new(binding.Config.WsaaUrl, binding.Config.CertificatePath,
        binding.Config.CertificatePassword, binding.CacheIdentity, serviceName: "wsfe");

static ApiKeyContextGrant[] ParseGrants(string? value)
{
    if (string.IsNullOrWhiteSpace(value)) return [];
    var parsed = new List<(string ContextId, string Operation)>();
    foreach (var item in ParseCsv(value))
    {
        var separator = item.LastIndexOf(':');
        if (separator <= 0 || separator == item.Length - 1)
            throw new ArgumentException($"Grant inválido '{item}'. Use contextId:operacion.");
        parsed.Add((item[..separator].Trim(), item[(separator + 1)..].Trim()));
    }

    return parsed
        .GroupBy(x => x.ContextId, StringComparer.Ordinal)
        .Select(group => new ApiKeyContextGrant(
            group.Key,
            group.Select(x => x.Operation).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()))
        .OrderBy(x => x.ContextId, StringComparer.Ordinal)
        .ToArray();
}

static string[] ParseCsv(string value)
    => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.Ordinal)
        .ToArray();

static string RequiredOption(string[] args, string name)
    => Option(args, name) is { Length: > 0 } value
        ? value
        : throw new ArgumentException($"Falta {name} <valor>.");

static string? Option(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1].Trim() : null;
}

static int ParseInt(string value, string option)
    => int.TryParse(value, out var parsed) && parsed > 0
        ? parsed
        : throw new ArgumentException($"{option} debe ser un entero positivo.");

static long ParseLong(string value, string option)
    => long.TryParse(value, out var parsed) && parsed > 0
        ? parsed
        : throw new ArgumentException($"{option} debe ser un entero positivo.");

static bool ParseBool(string value, string option)
    => bool.TryParse(value, out var parsed)
        ? parsed
        : throw new ArgumentException($"{option} debe ser true o false.");

static void PrintUsage()
{
    Console.Error.WriteLine(
        "dcArca.Cli <comando> [argumentos]\n" +
        "  facturar [archivo.json]\n" +
        "  ultimo-autorizado <tipoComprobante>\n" +
        "  consultar <numero> <tipoComprobante>\n" +
        "  padron <cuit>\n" +
        "  condiciones-iva <docTipo> <docNro> <tipoComprobante>\n" +
        "  create-key --name <nombre> --scope <scope1,scope2> [--consumer <consumerId> --grant <context:operacion,...>]\n" +
        "  set-key-grants <keyId> --consumer <consumerId> --grant <context:operacion,...>\n" +
        "  list-keys\n" +
        "  revoke-key <id>\n" +
        "  diagnose-v2 --context <ctx> --cuit <cuit> --point-of-sale <pv> --tipo-comprobante <tipo>\n" +
        "  list-contexts\n" +
        "  add-context --id <contextId> --environment <ambiente> [--context-revision <n>]\n" +
        "  add-assignment --context <contextId> --revision <rev> --credential <credentialId> --actor <actor>\n" +
        "  validate-assignment --context <contextId> --revision <rev> --actor <actor>\n" +
        "  activate-assignment --context <contextId> --revision <rev> --actor <actor>\n" +
        "  disable-assignment --context <contextId> --revision <rev> --actor <actor>\n" +
        "  set-context-state --context <contextId> --state <Active|ReadOnly|Disabled> --actor <actor>\n" +
    "  register-representation --context <ctx> --consumer <consumer> --cuit <cuit> --actor <actor>\n" +
    "  list-points-of-sale --context <ctx> --consumer <consumer> --cuit <cuit>\n" +
    "  select-representation-pv --context <ctx> --consumer <consumer> --cuit <cuit> --point-of-sale <pv> --actor <actor>\n" +
    "  activate-representation --context <ctx> --consumer <consumer> --cuit <cuit> --point-of-sale <pv> --actor <actor>\n" +
    "  revoke-representation --context <ctx> --consumer <consumer> --cuit <cuit> --point-of-sale <pv> --actor <actor>\n" +
    "  list-representations --context <ctx>");
}
