using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using _20262.Models.Api;
using _20262.Models.Entities;
using _20262.Services;

namespace _20262.Controllers;

[ApiController]
[Route("api/productos")]
[Produces("application/json")]
public class ProductosApiController : ControllerBase
{
    private readonly ProductoService _productoService;

    public ProductosApiController(ProductoService productoService)
    {
        _productoService = productoService;
    }

    [HttpGet]
    [ProducesResponseType<IEnumerable<ProductoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProductoDto>>> GetProductos(
        [FromQuery] int? categoriaId, [FromQuery] string? busqueda)
    {
        var productos = await _productoService.ObtenerProductosAsync();

        IEnumerable<Producto> resultado = productos;

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var termino = busqueda.Trim();
            resultado = resultado.Where(p => p.Nombre.Contains(termino, StringComparison.OrdinalIgnoreCase));
        }

        if (categoriaId.HasValue)
        {
            resultado = resultado.Where(p => p.CategoriaId == categoriaId.Value);
        }

        return Ok(resultado.Select(ProductoDto.FromEntity));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<ProductoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductoDto>> GetProducto(int id)
    {
        var producto = await _productoService.ObtenerProductoAsync(id);
        if (producto is null)
        {
            return NotFound();
        }

        return Ok(ProductoDto.FromEntity(producto));
    }

    [Authorize]
    [HttpPost]
    [ProducesResponseType<ProductoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProductoDto>> CreateProducto(ProductoDto dto)
    {
        if (!await _productoService.ExisteCategoriaAsync(dto.CategoriaId))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Categoría no encontrada",
                detail: $"La categoría {dto.CategoriaId} no existe.");
        }

        var producto = await _productoService.CrearProductoAsync(new Producto
        {
            Nombre = dto.Nombre,
            CategoriaId = dto.CategoriaId,
            Precio = dto.Precio,
            Descripcion = dto.Descripcion,
            ImagenUrl = dto.ImagenUrl,
            Stock = dto.Stock
        });

        return CreatedAtAction(nameof(GetProducto), new { id = producto.Id }, ProductoDto.FromEntity(producto));
    }

    [Authorize]
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProducto(int id, ProductoDto dto)
    {
        if (id != dto.Id)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "ID no coincide",
                detail: "El ID de la ruta no coincide con el ID del cuerpo.");
        }

        if (!await _productoService.ExisteCategoriaAsync(dto.CategoriaId))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Categoría no encontrada",
                detail: $"La categoría {dto.CategoriaId} no existe.");
        }

        var producto = await _productoService.ObtenerProductoAsync(id);
        if (producto is null)
        {
            return NotFound();
        }

        await _productoService.ActualizarProductoAsync(new Producto
        {
            Id = id,
            Nombre = dto.Nombre,
            CategoriaId = dto.CategoriaId,
            Precio = dto.Precio,
            Descripcion = dto.Descripcion,
            ImagenUrl = dto.ImagenUrl,
            Stock = dto.Stock
        });

        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProducto(int id)
    {
        var eliminado = await _productoService.EliminarProductoAsync(id);
        if (!eliminado)
        {
            return NotFound();
        }

        return NoContent();
    }
}