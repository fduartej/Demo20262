using System.Text.Json;
using Microsoft.Extensions.Options;

namespace _20262.Services;

public class PieSocketService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PieSocketOptions _options;
    private readonly ILogger<PieSocketService> _logger;

    public PieSocketService(
        IHttpClientFactory httpClientFactory,
        IOptions<PieSocketOptions> options,
        ILogger<PieSocketService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> PublicarStockReducidoAsync(int productoId, int stock, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                key = _options.ApiKey,
                secret = _options.Secret,
                roomId = _options.RoomId,
                message = new
                {
                    @event = "stock-reducido",
                    data = new { productoId, stock }
                }
            };

            using var client = _httpClientFactory.CreateClient();
            using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), System.Text.Encoding.UTF8, "application/json");
            using var response = await client.PostAsync($"https://{_options.ClusterId}.piesocket.com/api/publish", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("PieSocket: no se pudo publicar el stock reducido (HTTP {Status}).", (int)response.StatusCode);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PieSocket: error al publicar el stock reducido del producto {ProductoId}.", productoId);
            return false;
        }
    }
}