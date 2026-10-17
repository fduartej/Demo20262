namespace _20262.Integrations.Groq;

public interface IGroqChatService
{
    bool EstaConfigurado { get; }

    string Modelo { get; }

    /// <summary>Envía la conversación a Groq y transmite la respuesta token a token.</summary>
    IAsyncEnumerable<string> TransmitirRespuestaAsync(
        IReadOnlyList<MensajeChat> mensajes,
        CancellationToken cancellationToken = default);
}
