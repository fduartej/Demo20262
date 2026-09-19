namespace _20262.Models.Api;

/// <summary>Mensaje que viaja por la cola de CloudAMQP (ORDEN_REGISTRADA).</summary>
public class PedidoEnCola
{
    public Guid OrdenId { get; set; }

    public int ProductoId { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public decimal Precio { get; set; }

    public int Cantidad { get; set; }

    public decimal Total { get; set; }

    public DateTime CreadoEn { get; set; }
}