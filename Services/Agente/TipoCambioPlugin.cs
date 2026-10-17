using System.ComponentModel;
using System.Net;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace _20262.Services.Agente;

/// <summary>
/// Función del agente para consultar el tipo de cambio con la API pública Frankfurter
/// (https://api.frankfurter.dev/v2/rate/USD/PEN).
/// </summary>
public sealed class TipoCambioPlugin
{
    private readonly HttpClient _http;
    private readonly ILogger<TipoCambioPlugin> _logger;

    public TipoCambioPlugin(HttpClient http, ILogger<TipoCambioPlugin> logger)
    {
        _http = http;
        _logger = logger;
    }

    [KernelFunction("ObtenerTipoCambio")]
    [Description(
        "Obtiene el tipo de cambio oficial entre dos monedas usando la API Frankfurter. Por defecto convierte " +
        "de USD (dólar) a PEN (sol peruano). Úsala cuando pregunten por el tipo de cambio, la cotización del " +
        "dólar o la conversión de una moneda.")]
    public async Task<string> ObtenerTipoCambioAsync(
        [Description("Código ISO de 3 letras de la moneda de origen, por ejemplo USD.")] string monedaOrigen = "USD",
        [Description("Código ISO de 3 letras de la moneda de destino, por ejemplo PEN.")] string monedaDestino = "PEN",
        CancellationToken cancellationToken = default)
    {
        var origen = Normalizar(monedaOrigen);
        var destino = Normalizar(monedaDestino);

        if (origen is null || destino is null)
        {
            return "Indica monedas válidas con su código ISO de 3 letras, por ejemplo USD y PEN.";
        }

        try
        {
            using var respuesta = await _http.GetAsync($"v2/rate/{origen}/{destino}", cancellationToken);

            if (respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                return $"No hay tipo de cambio disponible para el par {origen}/{destino}.";
            }

            if (!respuesta.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Frankfurter | Error {Codigo} al consultar {Origen}/{Destino}.",
                    (int)respuesta.StatusCode, origen, destino);
                return "No se pudo obtener el tipo de cambio en este momento.";
            }

            using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(cancellationToken));
            var raiz = documento.RootElement;

            if (!raiz.TryGetProperty("rate", out var tasa) || !tasa.TryGetDouble(out var valor) || valor <= 0)
            {
                return "El servicio de tipo de cambio devolvió una respuesta con un formato inesperado.";
            }

            var fecha = raiz.TryGetProperty("date", out var fechaElemento) ? fechaElemento.GetString() : null;
            var vigencia = string.IsNullOrWhiteSpace(fecha) ? string.Empty : $" al {fecha}";

            return $"Tipo de cambio{vigencia}: 1 {origen} = {valor:0.0000} {destino} " +
                   $"(1 {destino} = {1 / valor:0.0000} {origen}).";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Frankfurter | No se pudo consultar {Origen}/{Destino}.", origen, destino);
            return "No se pudo consultar el tipo de cambio en este momento.";
        }
    }

    private static string? Normalizar(string? moneda)
    {
        var codigo = moneda?.Trim().ToUpperInvariant();
        return codigo is { Length: 3 } && codigo.All(char.IsLetter) ? codigo : null;
    }
}
