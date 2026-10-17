using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using _20262.Integrations.Groq;
using _20262.Models.Api;
using _20262.Services;

namespace _20262.Controllers;

/// <summary>
/// Asistente de la tienda. Disponible para cualquier visitante (admin o sin sesión): envía la pregunta
/// al agente de Semantic Kernel (base de conocimiento en Qdrant, tipo de cambio y catálogo de productos)
/// y transmite la respuesta en streaming junto con su mensaje de control (fuentes y tarjetas de producto).
/// </summary>
[AllowAnonymous]
public class AsistenteController : Controller
{
    private const string ClaveHistorial = "AsistenteHistorial";
    private const int MaxMensajesHistorial = 12;
    private const int MaxCaracteresPregunta = 2000;

    private readonly IAsistenteTiendaService _asistente;
    private readonly ILogger<AsistenteController> _logger;

    public AsistenteController(IAsistenteTiendaService asistente, ILogger<AsistenteController> logger)
    {
        _asistente = asistente;
        _logger = logger;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task Preguntar([FromBody] PreguntaAsistenteRequest? peticion, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/plain; charset=utf-8";
        Response.Headers.CacheControl = "no-cache";

        if (!_asistente.EstaDisponible)
        {
            await Response.WriteAsync("El asistente no está disponible en este momento.", cancellationToken);
            return;
        }

        var pregunta = peticion?.Mensaje?.Trim() ?? string.Empty;
        if (pregunta.Length == 0)
        {
            await Response.WriteAsync("Escribe una pregunta sobre la tienda.", cancellationToken);
            return;
        }

        if (pregunta.Length > MaxCaracteresPregunta)
        {
            pregunta = pregunta[..MaxCaracteresPregunta];
        }

        var historial = LeerHistorial();

        // La sesión debe quedar establecida antes de iniciar el streaming: la cookie de sesión solo
        // puede enviarse en las cabeceras, que se emiten con el primer fragmento de la respuesta.
        if (historial.Count == 0)
        {
            HttpContext.Session.SetString(ClaveHistorial, "[]");
        }

        var respuesta = new StringBuilder();

        try
        {
            var resultado = await _asistente.ResponderAsync(historial, pregunta, cancellationToken);

            await foreach (var fragmento in resultado.Fragmentos)
            {
                respuesta.Append(fragmento);
                await Response.WriteAsync(fragmento, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al responder en el asistente de la tienda.");

            if (respuesta.Length == 0)
            {
                await Response.WriteAsync(
                    "Lo siento, ocurrió un error al generar la respuesta. Intenta nuevamente.", cancellationToken);
            }

            return;
        }

        if (respuesta.Length > 0)
        {
            // El historial guarda solo el texto: los mensajes de control (fuentes y tarjetas) no son del modelo.
            GuardarHistorial(historial, pregunta, ProtocoloChatAsistente.Limpiar(respuesta.ToString()));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Limpiar()
    {
        HttpContext.Session.Remove(ClaveHistorial);
        return NoContent();
    }

    private List<MensajeChat> LeerHistorial()
    {
        var json = HttpContext.Session.GetString(ClaveHistorial);
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<MensajeChat>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void GuardarHistorial(List<MensajeChat> historial, string pregunta, string respuesta)
    {
        historial.Add(new MensajeChat { Rol = "user", Contenido = pregunta });
        historial.Add(new MensajeChat { Rol = "assistant", Contenido = respuesta });

        if (historial.Count > MaxMensajesHistorial)
        {
            historial = historial.Skip(historial.Count - MaxMensajesHistorial).ToList();
        }

        try
        {
            HttpContext.Session.SetString(ClaveHistorial, JsonSerializer.Serialize(historial));
        }
        catch (InvalidOperationException ex)
        {
            // La respuesta ya fue enviada; perder el historial no debe cortar la conexión del chat.
            _logger.LogWarning(ex, "No se pudo guardar el historial del asistente en la sesión.");
        }
    }
}
