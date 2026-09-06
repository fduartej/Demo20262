using Microsoft.AspNetCore.Mvc;
using _20262.Models.ViewModels;

namespace _20262.Controllers;

public class MascotasController : Controller
{
    private static readonly string[] AvailablePets = ["Perro", "Gato", "Pez", "Loro", "Hamster", "Conejo", "Tortuga"];

    public IActionResult Index()
    {
        return View(BuildPetModel([]));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Index(PetViewModel model)
    {
        return View(BuildPetModel(model.SelectedPets));
    }

    private static PetViewModel BuildPetModel(IEnumerable<string> selected)
    {
        var selectedSet = selected.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new PetViewModel
        {
            Pets = AvailablePets
                .Select(p => new PetOption { Name = p, Selected = selectedSet.Contains(p) })
                .ToList()
        };
    }
}
