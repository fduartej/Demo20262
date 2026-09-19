using _20262.Models.Api;

namespace _20262.Services;

public interface IOrdenPublisher
{
    /// <summary>Publica una orden de compra en la cola ORDEN_REGISTRADA de CloudAMQP.</summary>
    Task<bool> PublicarPedidoAsync(PedidoEnCola pedido, CancellationToken cancellationToken = default);
}