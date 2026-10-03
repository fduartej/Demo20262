using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using _20262.Data;
using _20262.Models.Entities;
using _20262.Models.ViewModels;
using _20262.Services;

namespace _20262.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ISentimentAnalysisService _sentimentService;

    public HomeController(ApplicationDbContext context, ISentimentAnalysisService sentimentService)
    {
        _context = context;
        _sentimentService = sentimentService;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Contact()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(ContactViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Clasifica el mensaje con el modelo de sentimiento (ML.NET). Si el modelo no está
        // disponible el contacto se guarda igual, pero sin sentimiento.
        var sentimiento = await _sentimentService.AnalizarAsync(model.Message, cancellationToken);

        var contacto = new Contacto
        {
            Name = model.Name,
            Email = model.Email,
            Message = model.Message,
            FechaRegistro = DateTime.Now,
            Sentimiento = sentimiento.Etiqueta,
            ProbabilidadSentimiento = sentimiento.Analizado ? sentimiento.Probabilidad : null
        };

        _context.Contactos.Add(contacto);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["ContactSuccess"] = "Gracias por contactarnos, " + model.Name + ". Hemos recibido su mensaje.";
        return RedirectToAction(nameof(Contact));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
