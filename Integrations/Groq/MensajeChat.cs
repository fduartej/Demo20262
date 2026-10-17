namespace _20262.Integrations.Groq;

/// <summary>Mensaje del historial de conversación (rol: system, user o assistant).</summary>
public class MensajeChat
{
    public string Rol { get; set; } = "user";

    public string Contenido { get; set; } = string.Empty;
}
