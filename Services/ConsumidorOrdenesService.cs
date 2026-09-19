using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using _20262.Data;
using _20262.Models.Api;
using _20262.Models.Entities;

namespace _20262.Services;

/// <summary>
/// Componente desacoplado que lee los pedidos de la cola ORDEN_REGISTRADA de CloudAMQP,
/// simula un procesamiento (delay), los registra en la tabla t_pedidos_registrados
/// y notifica el resultado por websocket.
/// </summary>
public class ConsumidorOrdenesService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly CloudAmqpOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConsumidorOrdenesService> _logger;
    private readonly Random _random = new();

    public ConsumidorOrdenesService(
        IOptions<CloudAmqpOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<ConsumidorOrdenesService> logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            IConnection? connection = null;
            IChannel? channel = null;

            try
            {
                var factory = new ConnectionFactory
                {
                    Uri = new Uri(_options.Uri),
                    AutomaticRecoveryEnabled = true,
                    ClientProvidedName = "MundoMascota-Consumidor"
                };

                connection = await factory.CreateConnectionAsync("MundoMascota-Consumidor", stoppingToken);
                channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await channel.QueueDeclareAsync(
                    queue: _options.QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: stoppingToken);

                // Procesa un mensaje a la vez para que el retardo sea visible.
                await channel.BasicQosAsync(0, 1, false, stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, ea) => ProcesarMensajeAsync(channel, ea, stoppingToken);
                await channel.BasicConsumeAsync(queue: _options.QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

                _logger.LogInformation("Consumidor de la cola '{Queue}' en espera de mensajes...", _options.QueueName);

                var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.ConnectionShutdownAsync += (_, _) =>
                {
                    tcs.TrySetResult();
                    return Task.CompletedTask;
                };

                try
                {
                    await tcs.Task.WaitAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error en el consumidor de la cola '{Queue}'; reintentando en 5s...", _options.QueueName);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            finally
            {
                if (channel is not null)
                {
                    await channel.DisposeAsync();
                }

                if (connection is not null)
                {
                    await connection.DisposeAsync();
                }
            }
        }
    }

    private async Task ProcesarMensajeAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken cancellationToken)
    {
        try
        {
            var body = Encoding.UTF8.GetString(ea.Body.Span);
            var pedido = JsonSerializer.Deserialize<PedidoEnCola>(body, JsonOptions);
            if (pedido is null)
            {
                throw new InvalidOperationException("El mensaje recibido no es un PedidoEnCola válido.");
            }

            _logger.LogInformation("MENSAJE RECIBIDO de la cola '{Queue}': orden {OrdenId} del producto {ProductoId} '{Nombre}'.",
                _options.QueueName, pedido.OrdenId, pedido.ProductoId, pedido.Nombre);

            // Delay intencional para que se aprecie el desacoplamiento productor -> consumidor.
            var delay = _random.Next(_options.DelayMinSeconds, _options.DelayMaxSeconds + 1);
            _logger.LogInformation("Procesando orden {OrdenId}: esperando {Delay}s (registro asíncrono)...", pedido.OrdenId, delay);
            await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pieSocket = scope.ServiceProvider.GetRequiredService<PieSocketService>();

            var registrado = new PedidoRegistrado
            {
                OrdenId = pedido.OrdenId,
                ProductoId = pedido.ProductoId,
                NombreProducto = pedido.Nombre,
                Precio = pedido.Precio,
                Cantidad = pedido.Cantidad,
                Total = pedido.Total,
                CreadoEn = pedido.CreadoEn,
                RegistradoEn = DateTime.UtcNow,
                Estado = "Registrado"
            };

            db.PedidosRegistrados.Add(registrado);
            await db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Orden {OrdenId} REGISTRADA en t_pedidos_registrados (pedido #{PedidoId}) del producto {ProductoId}.",
                pedido.OrdenId, registrado.Id, pedido.ProductoId);

            var notificado = await pieSocket.PublicarOrdenRegistradaAsync(
                registrado.Id, pedido.OrdenId, pedido.ProductoId, pedido.Nombre, pedido.Total, registrado.RegistradoEn, cancellationToken);

            _logger.LogInformation("Notificación websocket de la orden {OrdenId}: {Resultado}.", pedido.OrdenId, notificado ? "enviada" : "falló");

            await channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);
            _logger.LogInformation("Mensaje de la orden {OrdenId} confirmado (ack).", pedido.OrdenId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando el mensaje de la cola '{Queue}'; se reencolará.", _options.QueueName);

            try
            {
                await channel.BasicNackAsync(ea.DeliveryTag, false, true, cancellationToken);
            }
            catch (Exception nackEx)
            {
                _logger.LogWarning(nackEx, "No se pudo reencolar el mensaje {DeliveryTag}.", ea.DeliveryTag);
            }
        }
    }
}