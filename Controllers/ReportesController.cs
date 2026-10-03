using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using _20262.Data;
using _20262.Models.ViewModels;
using _20262.Services;

namespace _20262.Controllers;

/// <summary>Reportes administrativos: mensajes de contacto del día con el sentimiento detectado por ML.NET.</summary>
[Authorize(Roles = "Admin")]
public class ReportesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ISentimentAnalysisService _sentimentService;
    private readonly ILogger<ReportesController> _logger;

    public ReportesController(
        ApplicationDbContext context,
        ISentimentAnalysisService sentimentService,
        ILogger<ReportesController> logger)
    {
        _context = context;
        _sentimentService = sentimentService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Contactos(DateTime? fecha, string? filtro)
    {
        var dia = (fecha ?? DateTime.Today).Date;
        var diaSiguiente = dia.AddDays(1);
        var filtroSentimiento = string.IsNullOrWhiteSpace(filtro) ? "todos" : filtro.Trim().ToLowerInvariant();

        // Rango del día en hora local: la columna FechaRegistro se guarda como texto ISO en SQLite,
        // por lo que la comparación directa de DateTime también funciona en consultas en memoria.
        var mensajes = await _context.Contactos
            .AsNoTracking()
            .Where(c => c.FechaRegistro >= dia && c.FechaRegistro < diaSiguiente)
            .OrderByDescending(c => c.FechaRegistro)
            .Select(c => new ContactoReporteViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Email = c.Email,
                Message = c.Message,
                FechaRegistro = c.FechaRegistro,
                Sentimiento = c.Sentimiento,
                Probabilidad = c.ProbabilidadSentimiento
            })
            .ToListAsync();

        _logger.LogInformation(
            "Reportes.Contactos | fecha={Fecha} total={Total} positivos={Positivos} negativos={Negativos}",
            dia.ToString("yyyy-MM-dd"), mensajes.Count,
            mensajes.Count(m => m.EsPositivo), mensajes.Count(m => m.Sentimiento == "Negativo"));

        return View(new ReporteContactosViewModel
        {
            Fecha = dia,
            Filtro = filtroSentimiento,
            Mensajes = mensajes
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReentrenarModelo()
    {
        var ok = await _sentimentService.ReentrenarAsync(HttpContext.RequestAborted);

        TempData[ok ? "ModeloOk" : "ModeloError"] = ok
            ? "Modelo de sentimiento reentrenado correctamente."
            : "No se pudo reentrenar el modelo. Revisa el log y la configuración Sentimiento.";

        return RedirectToAction(nameof(Contactos));
    }
}
