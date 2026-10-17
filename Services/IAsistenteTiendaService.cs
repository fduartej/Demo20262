using _20262.Integrations.Groq;

namespace _20262.Services;

/// <summary>Respuesta del asistente: fuentes recuperadas + flujo de texto generado.</summary>
public class RespuestaAsistente
{
    public IReadOnlyList<string> Fuentes { get; init; } = [];

    public bool HayContexto { get; init; }

    public required IAsyncEnumerable<string> Fragmentos { get; init; }
}

public interface IAsistenteTiendaService
{
    bool EstaDisponible { get; }

    string Modelo { get; }

    /// <summary>
    /// Recupera el contexto de la base de conocimiento y prepara la respuesta del asistente en streaming.
    /// </summary>
    Task<RespuestaAsistente> ResponderAsync(
        IReadOnlyList<MensajeChat> historial,
        string pregunta,
        CancellationToken cancellationToken = default);
}
