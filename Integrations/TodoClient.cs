using System.Net.Http.Json;
using _20262.Models.Entities;

namespace _20262.Integrations;

public interface ITodoClient
{
    Task<List<TodoItem>> ObtenerTodosAsync(CancellationToken cancellationToken = default);
    Task<TodoItem?> ObtenerTodoAsync(int id, CancellationToken cancellationToken = default);
}

public class TodoClient : ITodoClient
{
    private readonly HttpClient _client;
    private readonly ILogger<TodoClient> _logger;

    public TodoClient(HttpClient client, ILogger<TodoClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<List<TodoItem>> ObtenerTodosAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var respuesta = await _client.GetFromJsonAsync<TodoListResponse>("todos", cancellationToken);
            return respuesta?.Todos ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al consultar los todos desde {BaseAddress}.", _client.BaseAddress);
            throw;
        }
    }

    public async Task<TodoItem?> ObtenerTodoAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.GetFromJsonAsync<TodoItem>($"todos/{id}", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al consultar el todo {Id} desde {BaseAddress}.", id, _client.BaseAddress);
            throw;
        }
    }

    private sealed class TodoListResponse
    {
        public List<TodoItem> Todos { get; set; } = [];
        public int Total { get; set; }
        public int Skip { get; set; }
        public int Limit { get; set; }
    }
}