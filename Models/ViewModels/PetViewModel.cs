namespace _20262.Models.ViewModels;

public class PetViewModel
{
    public List<PetOption> Pets { get; set; } = [];

    public List<string> SelectedPets { get; set; } = [];

    public bool HasSelection => Pets.Any(p => p.Selected);
}

public class PetOption
{
    public string Name { get; set; } = string.Empty;

    public bool Selected { get; set; }
}
