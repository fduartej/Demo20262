namespace _20262.Services;

public class CloudAmqpOptions
{
    public const string SectionName = "CloudAmqp";

    public string Uri { get; set; } = string.Empty;

    public string QueueName { get; set; } = "ORDEN_REGISTRADA";

    /// <summary>Retardo mínimo (segundos) que simula el procesamiento del consumidor.</summary>
    public int DelayMinSeconds { get; set; } = 3;

    /// <summary>Retardo máximo (segundos) que simula el procesamiento del consumidor.</summary>
    public int DelayMaxSeconds { get; set; } = 6;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Uri) && !string.IsNullOrWhiteSpace(QueueName);
}