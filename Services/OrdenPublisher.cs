using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using _20262.Models.Api;

namespace _20262.Services;

public class OrdenPublisher : IOrdenPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly CloudAmqpOptions _options;
    private readonly ILogger<OrdenPublisher> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private IConnection? _connection;

    public OrdenPublisher(IOptions<CloudAmqpOptions> options, ILogger<OrdenPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> PublicarPedidoAsync(PedidoEnCola pedido, CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("CloudAMQP no está configurado; no se pudo publicar el pedido {OrdenId}.", pedido.OrdenId);
            return false;
        }

        try
        {
            var connection = await ObtenerConexionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            // La cola se declara durable para que los pedidos sobrevivan a reinicios.
            await channel.QueueDeclareAsync(
                queue: _options.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(pedido, JsonOptions));

            var propiedades = new BasicProperties
            {
                Persistent = true,
                MessageId = pedido.OrdenId.ToString("N"),
                ContentType = "application/json"
            };

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: _options.QueueName,
                mandatory: true,
                basicProperties: propiedades,
                body: body,
                cancellationToken: cancellationToken);

            _logger.LogInformation("PEDIDO PUBLICADO en la cola '{Queue}': orden {OrdenId} del producto {ProductoId} '{Nombre}' (cantidad {Cantidad}, total S/ {Total:0.00}).",
                _options.QueueName, pedido.OrdenId, pedido.ProductoId, pedido.Nombre, pedido.Cantidad, pedido.Total);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al publicar el pedido {OrdenId} en la cola '{Queue}'.", pedido.OrdenId, _options.QueueName);
            return false;
        }
    }

    private async Task<IConnection> ObtenerConexionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            var factory = new ConnectionFactory
            {
                Uri = new Uri(_options.Uri),
                AutomaticRecoveryEnabled = true,
                ClientProvidedName = "MundoMascota-Publisher"
            };

            _connection = await factory.CreateConnectionAsync("MundoMascota-Publisher", cancellationToken);
            _logger.LogInformation("Conexión a CloudAMQP establecida (publisher).");
            return _connection;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _semaphore.Dispose();
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}