using Microsoft.AspNetCore.Mvc;
using _20262.Models.Api;
using _20262.Services;

namespace _20262.Controllers;

[ApiController]
[Route("api/pedidos")]
[Produces("application/json")]
public class PedidosApiController : ControllerBase
{
    private readonly ProductoService _productoService;
    private readonly IOrdenPublisher _ordenPublisher;
    private readonly ILogger<PedidosApiController> _logger;

    public PedidosApiController(
        ProductoService productoService,
        IOrdenPublisher ordenPublisher,
        ILogger<PedidosApiController> logger)
    {
        _productoService = productoService;
        _ordenPublisher = ordenPublisher;
        _logger = logger;
    }

    /// <summary>Recibe el producto a comprar y publica la orden de compra en la cola ORDEN_REGISTRADA.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Comprar([FromBody] PedidoRequest request, CancellationToken cancellationToken)
    {
        if (request is null || !ModelState.IsValid)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Solicitud inválida",
                detail: "Debe indicar un productoId válido.");
        }

        var producto = await _productoService.ObtenerProductoAsync(request.ProductoId, cancellationToken);
        if (producto is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Producto no encontrado",
                detail: $"El producto {request.ProductoId} no existe.");
        }

        if (producto.SinStock)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Sin stock",
                detail: $"El producto '{producto.Nombre}' no tiene stock disponible.");
        }

        var pedido = new PedidoEnCola
        {
            OrdenId = Guid.NewGuid(),
            ProductoId = producto.Id,
            Nombre = producto.Nombre,
            Precio = producto.Precio,
            Cantidad = request.Cantidad,
            Total = producto.Precio * request.Cantidad,
            CreadoEn = DateTime.UtcNow
        };

        _logger.LogInformation("Solicitud de compra recibida: producto {ProductoId} '{Nombre}', cantidad {Cantidad}.",
            producto.Id, producto.Nombre, request.Cantidad);

        var publicado = await _ordenPublisher.PublicarPedidoAsync(pedido, cancellationToken);
        if (!publicado)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Cola no disponible",
                detail: "No se pudo publicar la orden en la cola ORDEN_REGISTRADA.");
        }

        return Accepted(new
        {
            ordenId = pedido.OrdenId,
            productoId = pedido.ProductoId,
            nombre = pedido.Nombre,
            cantidad = pedido.Cantidad,
            total = pedido.Total,
            estado = "En cola"
        });
    }
}