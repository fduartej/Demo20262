using _20262.Models.Api;

namespace _20262.Services.Agente;

/// <summary>
/// Estado de una consulta al agente: acumula las fuentes de conocimiento usadas y los productos
/// encontrados, para enviarlos al cliente como mensaje de control (fuentes y tarjetas).
/// </summary>
public sealed class SesionAgente
{
    private const int MaxTarjetas = 4;

    private readonly List<string> _fuentes = [];
    private readonly List<TarjetaProducto> _productos = [];

    public bool ConocimientoConsultado { get; private set; }

    public IReadOnlyList<string> Fuentes => _fuentes;

    public IReadOnlyList<TarjetaProducto> Productos => _productos;

    public void MarcarConocimientoConsultado() => ConocimientoConsultado = true;

    public void AgregarFuente(string? nombre)
    {
        if (!string.IsNullOrWhiteSpace(nombre) &&
            !_fuentes.Contains(nombre, StringComparer.OrdinalIgnoreCase))
        {
            _fuentes.Add(nombre);
        }
    }

    public void AgregarProducto(TarjetaProducto producto)
    {
        if (_productos.Count >= MaxTarjetas || _productos.Any(p => p.Id == producto.Id))
        {
            return;
        }

        _productos.Add(producto);
    }
}
