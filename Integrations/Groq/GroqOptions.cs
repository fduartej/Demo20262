namespace _20262.Integrations.Groq;

public class GroqOptions
{
    public const string SectionName = "Groq";

    public string ApiKey { get; set; } = string.Empty;

    public string Organization { get; set; } = string.Empty;

    public string Model { get; set; } = "openai/gpt-oss-120b";

    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1";

    public bool EstaConfigurado => !string.IsNullOrWhiteSpace(ApiKey);
}
