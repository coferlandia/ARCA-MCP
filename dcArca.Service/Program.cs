/*
 * Copyright (c) 2025 Diego Cofré, DC Sistemas
 * www.diegocofre.com.ar
 *
 * Licensed under the Apache License, Version 2.0.
 * You may obtain a copy of the License at
 * http://www.apache.org/licenses/LICENSE-2.0
 */

using System.Text.Json;
using dcArca.Core.Models;
using dcArca.Core.Services;

var config = LoadConfig();
var isCli = args.Length > 0 && args[0] != "serve";

if (isCli)
{
    return await RunCliAsync(args, config);
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<IdcWsfeClient, dcWsfeClient>();
builder.Services.AddSingleton<IdcPadronClient, dcPadronClient>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/facturas", async (dcFacturaRequest factura, IdcWsfeClient client) =>
{
    var resultado = await client.FECAESolicitarAsync(factura);
    return resultado.Success ? Results.Ok(resultado) : Results.BadRequest(resultado);
});

app.MapGet("/api/facturas/ultimo-autorizado/{tipoComprobante}", async (dcTipoComprobante tipoComprobante, IdcWsfeClient client)
    => Results.Ok(await client.FECompUltimoAutorizadoAsync(tipoComprobante)));

app.MapGet("/api/comprobantes/{numero:long}/{tipoComprobante}", async (long numero, dcTipoComprobante tipoComprobante, IdcWsfeClient client)
    => Results.Ok(await client.FECompConsultarAsync(numero, tipoComprobante)));

app.MapGet("/api/padron/{cuit:long}", async (long cuit, IdcPadronClient client)
    => Results.Ok(await client.GetPersonaAsync(cuit)));

app.MapGet("/api/condiciones-iva", async (int docTipo, long docNro, dcTipoComprobante tipoComprobante, IdcWsfeClient client)
    => Results.Ok(await client.GetCondicionesIVAReceptorAsync(docTipo, docNro, tipoComprobante)));

app.Run();
return 0;

static dcArcaConfig LoadConfig()
{
    var configuration = new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true)
        .AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"), optional: true)
        .AddEnvironmentVariables()
        .Build();

    var config = new dcArcaConfig();
    configuration.GetSection("dcArcaConfig").Bind(config);
    return config;
}

static async Task<int> RunCliAsync(string[] args, dcArcaConfig config)
{
    IdcWsfeClient wsfe = new dcWsfeClient(config);
    IdcPadronClient padron = new dcPadronClient(config);
    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

    try
    {
        object result = args[0] switch
        {
            "facturar" => await FacturarAsync(args, wsfe),
            "ultimo-autorizado" => await wsfe.FECompUltimoAutorizadoAsync((dcTipoComprobante)int.Parse(args[1])),
            "consultar" => await wsfe.FECompConsultarAsync(long.Parse(args[1]), (dcTipoComprobante)int.Parse(args[2])),
            "padron" => await padron.GetPersonaAsync(long.Parse(args[1])),
            "condiciones-iva" => await wsfe.GetCondicionesIVAReceptorAsync(int.Parse(args[1]), long.Parse(args[2]), (dcTipoComprobante)int.Parse(args[3])),
            _ => throw new ArgumentException(
                $"Comando desconocido: {args[0]}. Comandos disponibles: " +
                "facturar [archivo.json], ultimo-autorizado <tipoComprobante>, " +
                "consultar <numero> <tipoComprobante>, padron <cuit>, " +
                "condiciones-iva <docTipo> <docNro> <tipoComprobante>, serve")
        };
        Console.WriteLine(JsonSerializer.Serialize(result, jsonOptions));
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
}

static async Task<dcFacturaResponse> FacturarAsync(string[] args, IdcWsfeClient wsfe)
{
    var json = args.Length > 1 && File.Exists(args[1])
        ? await File.ReadAllTextAsync(args[1])
        : await Console.In.ReadToEndAsync();

    var factura = JsonSerializer.Deserialize<dcFacturaRequest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new ArgumentException("JSON de factura inválido o vacío.");

    return await wsfe.FECAESolicitarAsync(factura);
}
