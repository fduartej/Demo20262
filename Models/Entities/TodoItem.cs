namespace _20262.Models.Entities;

public class TodoItem
{
    public int Id { get; set; }
    public string Todo { get; set; } = string.Empty;
    public bool Completed { get; set; }
    public int UserId { get; set; }
}