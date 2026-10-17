using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace _20262.Integrations.Groq;

public class GroqChatService : IGroqChatService
{
    private readonly HttpClient _http;
    private readonly GroqOptions _options;
    private readonly ILogger<GroqChatService> _logger;

    public GroqChatService(
        HttpClient http,
        IOptions<GroqOptions> options,
        ILogger<GroqChatService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        if (_options.EstaConfigurado)
        {
            _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            if (!string.IsNullOrWhiteSpace(_options.Organization))
            {
                _http.DefaultRequestHeaders.TryAddWithoutValidation("OpenAI-Organization", _options.Organization);
            }
        }
    }

    public bool EstaConfigurado => _options.EstaConfigurado;

    public string Modelo => _options.Model;

    public async IAsyncEnumerable<string> TransmitirRespuestaAsync(
        IReadOnlyList<MensajeChat> mensajes,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!EstaConfigurado)
        {
            throw new InvalidOperationException(
                "Groq no está configurado. Completa ApiKey y Organization en la sección Groq de appsettings.json.");
        }

        var peticion = new
        {
            model = _options.Model,
            temperature = 0,
            stream = true,
            messages = mensajes.Select(m => new { role = m.Rol, content = m.Contenido }).ToList()
        };

        using var solicitud = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(peticion)
        };
        solicitud.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var respuesta = await _http.SendAsync(
            solicitud, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalle = await respuesta.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Groq | Error {Codigo}: {Detalle}", (int)respuesta.StatusCode, detalle);
            throw new InvalidOperationException($"El asistente no está disponible (HTTP {(int)respuesta.StatusCode}).");
        }

        await using var flujo = await respuesta.Content.ReadAsStreamAsync(cancellationToken);
        using var lector = new StreamReader(flujo, Encoding.UTF8);

        while (await lector.ReadLineAsync(cancellationToken) is { } linea)
        {
            if (!linea.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var datos = linea["data:".Length..].Trim();
            if (datos.Length == 0)
            {
                continue;
            }

            if (datos == "[DONE]")
            {
                yield break;
            }

            string? fragmento = null;
            try
            {
                using var documento = JsonDocument.Parse(datos);
                if (documento.RootElement.TryGetProperty("choices", out var opciones) &&
                    opciones.ValueKind == JsonValueKind.Array &&
                    opciones.GetArrayLength() > 0 &&
                    opciones[0].TryGetProperty("delta", out var delta) &&
                    delta.ValueKind == JsonValueKind.Object &&
                    delta.TryGetProperty("content", out var contenido))
                {
                    fragmento = contenido.GetString();
                }
            }
            catch (JsonException)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(fragmento))
            {
                yield return fragmento;
            }
        }
    }
}
