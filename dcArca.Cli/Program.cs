/*
 * Copyright (c) 2025 Diego Cofré, DC Sistemas
 * www.diegocofre.com.ar
 *
 * Licensed under the Apache License, Version 2.0.
 */

using System.Text.Json;
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
    if (args[0] is "create-key" or "list-keys" or "revoke-key" or "set-key-grants")
        return await ManageApiKeysAsync(args, jsonOptions);

    if (args[0] is "list-contexts" or "add-context" or "add-assignment" or "validate-assignment"
        or "activate-assignment" or "disable-assignment" or "set-context-state")
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
    var store = new FileSystemRepresentedFiscalContextStore(configuration["FiscalContexts:Directory"]);

    switch (args[0])
    {
        case "list-contexts":
        {
            var contexts = await store.ListAsync();
            Console.WriteLine(JsonSerializer.Serialize(contexts, jsonOptions));
            return 0;
        }
        case "add-context":
        {
            var context = new RepresentedFiscalContextRecord(
                RequiredOption(args, "--id"),
                RequiredOption(args, "--environment"),
                ParseLong(RequiredOption(args, "--cuit"), "--cuit"),
                ParseInt(RequiredOption(args, "--point-of-sale"), "--point-of-sale"),
                FiscalContextOperationalState.Disabled,
                ParseInt(Option(args, "--context-revision") ?? "1", "--context-revision"),
                ParseBool(Option(args, "--legacy-default") ?? "false", "--legacy-default"),
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
            var context = await store.GetAsync(contextId)
                ?? throw new InvalidOperationException("FISCAL_CONTEXT_NOT_FOUND");
            var assignment = context.Assignments.SingleOrDefault(x => string.Equals(x.AssignmentRevision, revision, StringComparison.Ordinal))
                ?? throw new InvalidOperationException("ASSIGNMENT_NOT_FOUND");
            if (assignment.Status is not (CredentialAssignmentStatus.Candidate or CredentialAssignmentStatus.Validated))
                throw new InvalidOperationException("ASSIGNMENT_VALIDATION_STATE_INVALID");

            var legacyConfig = LoadArcaConfig(configuration);
            var legacyEnvironment = configuration["FiscalContext:Environment"]
                ?? (legacyConfig.WsfeUrl.Contains("homo", StringComparison.OrdinalIgnoreCase) ? "homologacion" : "produccion");
            var binding = new FiscalCredentialHostBindingResolver(configuration, legacyConfig, legacyEnvironment)
                .Resolve(context, assignment);
            var auth = new dcArcaAuthService(
                binding.Config.WsaaUrl,
                binding.Config.CertificatePath,
                binding.Config.CertificatePassword,
                binding.CacheIdentity,
                serviceName: "wsfe");
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var probe = await new dcWsfePointOfSaleProbe().ProbeAsync(binding.Config, auth, httpClient);
            var evidence = probe.Verified
                ? $"FEParamGetPtosVenta|context={context.ContextId}|assignment={assignment.AssignmentRevision}|pv={probe.PointOfSale}|emission={probe.EmissionType}|checked={probe.CheckedAt:O}"
                : null;
            if (probe.Verified && evidence is not null)
                await store.MarkAssignmentValidatedAsync(contextId, revision, evidence, actor);

            Console.WriteLine(JsonSerializer.Serialize(new
            {
                probe.Status,
                probe.Code,
                probe.SafeMessage,
                ContextId = contextId,
                AssignmentRevision = revision,
                assignment.CredentialId,
                probe.PointOfSale,
                probe.CheckedAt,
                Evidence = evidence
            }, jsonOptions));
            return probe.Verified ? 0 : 2;
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
            await store.SetOperationalStateAsync(contextId, state, actor);
            Console.WriteLine(JsonSerializer.Serialize(new { ContextId = contextId, State = state }, jsonOptions));
            return 0;
        }
        default:
            throw new ArgumentException($"Comando desconocido: {args[0]}");
    }
}

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
        "  list-contexts\n" +
        "  add-context --id <contextId> --environment <ambiente> --cuit <representado> --point-of-sale <pv> [--context-revision <n>]\n" +
        "  add-assignment --context <contextId> --revision <rev> --credential <credentialId> --actor <actor>\n" +
        "  validate-assignment --context <contextId> --revision <rev> --actor <actor>\n" +
        "  activate-assignment --context <contextId> --revision <rev> --actor <actor>\n" +
        "  disable-assignment --context <contextId> --revision <rev> --actor <actor>\n" +
        "  set-context-state --context <contextId> --state <Active|ReadOnly|Disabled> --actor <actor>");
}
