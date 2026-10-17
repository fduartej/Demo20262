using System.Text;
using _20262.Integrations.Groq;
using _20262.Integrations.Qdrant;

namespace _20262.Services;

/// <summary>Asistente de la tienda: combina la base de conocimiento en Qdrant con el modelo de Groq.</summary>
public class AsistenteTiendaService : IAsistenteTiendaService
{
    private const int FragmentosContexto = 5;

    private const string Instrucciones = """
        Eres el asistente virtual de la tienda "Mundo Mascota", un comercio de productos y servicios para mascotas.
        Ayudas a clientes y administradores respondiendo dudas sobre productos, categorías, pedidos, envíos, stock,
        políticas, recomendaciones y el funcionamiento general de la tienda.

        Reglas:
        - Si el usuario solo saluda (por ejemplo "hola", "buenas") o pregunta quién eres o qué puedes hacer, no
          necesitas contexto: preséntate como el asistente virtual de Mundo Mascota y menciona brevemente en qué
          puedes ayudar (productos y categorías, recomendaciones, pedidos y envíos, stock, políticas de la tienda).
        - Prioriza siempre el CONTEXTO recuperado de la base de conocimiento de la tienda que se te entrega.
        - Puedes complementar con conocimiento general sobre tiendas de mascotas y el funcionamiento habitual del
          comercio, pero NUNCA inventes datos específicos como precios, stock, tallas, plazos, promociones o políticas
          que no aparezcan en el contexto.
        - Si no tienes información suficiente para responder con certeza, dilo con claridad y sugiere contactar a la tienda.
        - Responde siempre en español, de forma clara, breve y amable. Usa listas cuando ayuden a la claridad.
        """;

    private readonly IGroqChatService _groq;
    private readonly IKnowledgeBaseService _conocimiento;
    private readonly ILogger<AsistenteTiendaService> _logger;

    public AsistenteTiendaService(
        IGroqChatService groq,
        IKnowledgeBaseService conocimiento,
        ILogger<AsistenteTiendaService> logger)
    {
        _groq = groq;
        _conocimiento = conocimiento;
        _logger = logger;
    }

    public bool EstaDisponible => _groq.EstaConfigurado;

    public string Modelo => _groq.Modelo;

    public async Task<RespuestaAsistente> ResponderAsync(
        IReadOnlyList<MensajeChat> historial,
        string pregunta,
        CancellationToken cancellationToken = default)
    {
        var contexto = await ObtenerContextoAsync(pregunta, cancellationToken);

        var mensajes = new List<MensajeChat>
        {
            new() { Rol = "system", Contenido = Instrucciones },
            new() { Rol = "system", Contenido = contexto.Texto }
        };
        mensajes.AddRange(historial);
        mensajes.Add(new MensajeChat { Rol = "user", Contenido = pregunta });

        return new RespuestaAsistente
        {
            Fuentes = contexto.Fuentes,
            HayContexto = contexto.HayContexto,
            Fragmentos = _groq.TransmitirRespuestaAsync(mensajes, cancellationToken)
        };
    }

    private async Task<ContextoRecuperado> ObtenerContextoAsync(string pregunta, CancellationToken cancellationToken)
    {
        if (!_conocimiento.EstaConfigurado)
        {
            return new ContextoRecuperado
            {
                Texto = "No hay base de conocimiento disponible en este momento.",
                HayContexto = false
            };
        }

        try
        {
            var resultados = await _conocimiento.BuscarAsync(pregunta, FragmentosContexto, cancellationToken);
            if (resultados.Count == 0)
            {
                return new ContextoRecuperado
                {
                    Texto = "No se encontraron fragmentos relevantes en la base de conocimiento de la tienda.",
                    HayContexto = false
                };
            }

            var contexto = new StringBuilder();
            contexto.AppendLine("CONTEXTO (base de conocimiento de la tienda):");
            foreach (var resultado in resultados)
            {
                contexto.AppendLine();
                contexto.AppendLine($"### Fuente: {resultado.Nombre} (relevancia {resultado.Puntaje:0.00})");
                contexto.AppendLine(resultado.Contenido);
            }

            var fuentes = resultados
                .Select(r => r.Nombre)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ContextoRecuperado
            {
                Texto = contexto.ToString(),
                Fuentes = fuentes,
                HayContexto = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo recuperar el contexto de la base de conocimiento.");
            return new ContextoRecuperado
            {
                Texto = "No se pudo recuperar la base de conocimiento en este momento.",
                HayContexto = false
            };
        }
    }

    private sealed class ContextoRecuperado
    {
        public string Texto { get; init; } = string.Empty;

        public IReadOnlyList<string> Fuentes { get; init; } = [];

        public bool HayContexto { get; init; }
    }
}
