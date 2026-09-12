using Microsoft.AspNetCore.Mvc;
using _20262.Integrations;

namespace _20262.Controllers;

public class TodosController : Controller
{
    private readonly ITodoClient _todoClient;

    public TodosController(ITodoClient todoClient)
    {
        _todoClient = todoClient;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var todos = await _todoClient.ObtenerTodosAsync(cancellationToken);
        return View(todos);
    }

    public async Task<IActionResult> Detalle(int id, CancellationToken cancellationToken)
    {
        var todo = await _todoClient.ObtenerTodoAsync(id, cancellationToken);
        if (todo is null)
        {
            return NotFound();
        }

        return View(todo);
    }
}