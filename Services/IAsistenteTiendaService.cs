using _20262.Integrations.Groq;

namespace _20262.Services;

/// <summary>Respuesta del asistente: flujo de texto generado (incluye los mensajes de control del protocolo).</summary>
public class RespuestaAsistente
{
    public required IAsyncEnumerable<string> Fragmentos { get; init; }
}

public interface IAsistenteTiendaService
{
    bool EstaDisponible { get; }

    string Modelo { get; }

    /// <summary>Prepara la respuesta del asistente en streaming usando el agente y sus herramientas.</summary>
    Task<RespuestaAsistente> ResponderAsync(
        IReadOnlyList<MensajeChat> historial,
        string pregunta,
        CancellationToken cancellationToken = default);
}
