using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace _20262.Models.Api;

/// <summary>
/// Protocolo de streaming del chat del asistente. El texto de la respuesta viaja tal cual y los
/// mensajes de control (fuentes, contexto y tarjetas de producto) van envueltos entre el
/// carácter STX (\u0002). El cliente los extrae del texto y los interpreta.
/// </summary>
public static class ProtocoloChatAsistente
{
    public const char Delimitador = '\u0002';

    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Envuelve un mensaje de control para enviarlo dentro del flujo de texto.</summary>
    public static string Envolver(object mensaje) =>
        $"{Delimitador}{JsonSerializer.Serialize(mensaje, Opciones)}{Delimitador}";

    /// <summary>Quita los mensajes de control de un texto (por ejemplo, para guardarlo en el historial).</summary>
    public static string Limpiar(string texto)
    {
        if (string.IsNullOrEmpty(texto) || texto.IndexOf(Delimitador) < 0)
        {
            return texto;
        }

        var limpio = new StringBuilder(texto.Length);
        var indice = 0;

        while (indice < texto.Length)
        {
            if (texto[indice] == Delimitador)
            {
                var cierre = texto.IndexOf(Delimitador, indice + 1);
                if (cierre < 0)
                {
                    break;
                }

                indice = cierre + 1;
                continue;
            }

            limpio.Append(texto[indice]);
            indice++;
        }

        return limpio.ToString();
    }
}

/// <summary>Mensaje de control que acompaña a la respuesta del asistente.</summary>
public sealed class MensajeControlAsistente
{
    /// <summary>Documentos de la base de conocimiento que se consultaron.</summary>
    public IReadOnlyList<string> Fuentes { get; init; } = [];

    /// <summary>Indica si el agente consultó la base de conocimiento de la tienda.</summary>
    public bool Contexto { get; init; }

    /// <summary>Productos encontrados para mostrar como tarjetas.</summary>
    public IReadOnlyList<TarjetaProducto> Productos { get; init; } = [];
}

/// <summary>Datos de un producto que el chat muestra como tarjeta.</summary>
public sealed class TarjetaProducto
{
    public int Id { get; init; }

    public string Nombre { get; init; } = string.Empty;

    public decimal Precio { get; init; }

    public int Stock { get; init; }

    public string Descripcion { get; init; } = string.Empty;

    public string ImagenUrl { get; init; } = string.Empty;

    public string? Categoria { get; init; }

    public static TarjetaProducto Desde(Models.Entities.Producto producto) => new()
    {
        Id = producto.Id,
        Nombre = producto.Nombre,
        Precio = producto.Precio,
        Stock = producto.Stock,
        Descripcion = producto.Descripcion,
        ImagenUrl = producto.ImagenUrl,
        Categoria = producto.Categoria?.Nombre
    };
}
