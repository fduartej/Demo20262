namespace _20262.Services;

public class PieSocketOptions
{
    public const string SectionName = "PieSocket";

    public string ClusterId { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string Secret { get; set; } = string.Empty;

    public string RoomId { get; set; } = string.Empty;

    public string WebSocketUrl { get; set; } = string.Empty;
}