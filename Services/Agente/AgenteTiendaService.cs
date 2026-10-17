using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using _20262.Integrations.Groq;
using _20262.Models.Api;

namespace _20262.Services.Agente;

/// <summary>
/// Agente de la tienda con Semantic Kernel. Sobre el modelo de Groq expone herramientas
/// (function calling) para consultar la base de conocimiento (Qdrant), el tipo de cambio
/// (Frankfurter) y el catálogo de productos (tabla t_productos).
/// </summary>
public sealed class AgenteTiendaService : IAsistenteTiendaService
{
    private const string Instrucciones = """
        Eres el asistente virtual de "Mundo Mascota", una tienda de productos y servicios para mascotas.
        Atiendes a clientes y visitantes en español, de forma clara, breve y amable.

        Tienes herramientas y debes usarlas así:
        - BuscarEnConocimiento: úsala SIEMPRE para responder preguntas sobre la tienda (qué es, qué hace,
          quién es, sus funcionalidades, servicios, políticas, envíos, pagos, horarios y preguntas frecuentes).
          Prioriza siempre lo que devuelva esa herramienta y no inventes datos que no aparezcan en ella.
        - ObtenerTipoCambio: úsala cuando pregunten por el tipo de cambio, la cotización del dólar o
          conversiones de moneda. Si no indican monedas, usa USD a PEN (soles peruanos).
        - BuscarProductos / ObtenerProductoPorId: úsalas cuando pregunten por productos, precios, stock o
          disponibilidad. Nunca inventes precios ni stock: si no encuentras el producto, dilo con claridad.

        Reglas:
        - No menciones nombres de herramientas, códigos ni JSON; responde como una conversación natural.
        - La interfaz mostrará automáticamente una tarjeta con la imagen y los datos del producto encontrado;
          no repitas la ficha completa en el texto, comenta lo relevante (precio, stock o una recomendación).
        - Si un producto no tiene stock, avísalo.
        - Si no tienes información suficiente para responder con certeza, dilo y sugiere contactar a la tienda.
        - Si solo te saludan o preguntan quién eres, preséntate y menciona brevemente en qué puedes ayudar
          (información de la tienda, productos y stock, y tipo de cambio).
        - Usa listas cuando ayuden a la claridad.
        """;

    private readonly GroqOptions _groq;
    private readonly ConocimientoPlugin _conocimiento;
    private readonly TipoCambioPlugin _tipoCambio;
    private readonly ProductosPlugin _productos;
    private readonly SesionAgente _sesion;
    private readonly ILogger<AgenteTiendaService> _logger;

    public AgenteTiendaService(
        IOptions<GroqOptions> groq,
        ConocimientoPlugin conocimiento,
        TipoCambioPlugin tipoCambio,
        ProductosPlugin productos,
        SesionAgente sesion,
        ILogger<AgenteTiendaService> logger)
    {
        _groq = groq.Value;
        _conocimiento = conocimiento;
        _tipoCambio = tipoCambio;
        _productos = productos;
        _sesion = sesion;
        _logger = logger;
    }

    public bool EstaDisponible => _groq.EstaConfigurado;

    public string Modelo => _groq.Model;

    public Task<RespuestaAsistente> ResponderAsync(
        IReadOnlyList<MensajeChat> historial,
        string pregunta,
        CancellationToken cancellationToken = default)
    {
        var kernel = ConstruirKernel();

        var conversacion = new ChatHistory(Instrucciones);
        foreach (var mensaje in historial)
        {
            var contenido = mensaje.Contenido ?? string.Empty;

            switch (mensaje.Rol)
            {
                case "assistant":
                    conversacion.AddAssistantMessage(contenido);
                    break;
                case "system":
                    conversacion.AddSystemMessage(contenido);
                    break;
                default:
                    conversacion.AddUserMessage(contenido);
                    break;
            }
        }

        conversacion.AddUserMessage(pregunta);

        var ajustes = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.1,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };

        return Task.FromResult(new RespuestaAsistente
        {
            Fragmentos = GenerarAsync(kernel, conversacion, ajustes, cancellationToken)
        });
    }

    private Kernel ConstruirKernel()
    {
        var builder = Kernel.CreateBuilder();

        builder.AddOpenAIChatCompletion(
            modelId: _groq.Model,
            endpoint: new Uri(_groq.BaseUrl),
            apiKey: _groq.ApiKey);

        builder.Plugins.AddFromObject(_conocimiento, "conocimiento");
        builder.Plugins.AddFromObject(_tipoCambio, "tipoCambio");
        builder.Plugins.AddFromObject(_productos, "productos");

        return builder.Build();
    }

    private async IAsyncEnumerable<string> GenerarAsync(
        Kernel kernel,
        ChatHistory conversacion,
        OpenAIPromptExecutionSettings ajustes,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var chat = kernel.GetRequiredService<IChatCompletionService>();

        _logger.LogDebug(
            "Agente | Consultando {Modelo} con {Mensajes} mensajes en la conversación.",
            _groq.Model, conversacion.Count);

        await foreach (var fragmento in chat.GetStreamingChatMessageContentsAsync(
            conversacion, ajustes, kernel, cancellationToken))
        {
            if (!string.IsNullOrEmpty(fragmento.Content))
            {
                yield return fragmento.Content;
            }
        }

        if (_sesion.ConocimientoConsultado || _sesion.Productos.Count > 0)
        {
            yield return ProtocoloChatAsistente.Envolver(new MensajeControlAsistente
            {
                Fuentes = _sesion.Fuentes,
                Contexto = _sesion.ConocimientoConsultado,
                Productos = _sesion.Productos
            });
        }
    }
}
