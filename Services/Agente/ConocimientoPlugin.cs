using System.ComponentModel;
using System.Text;
using Microsoft.SemanticKernel;
using _20262.Integrations.Qdrant;

namespace _20262.Services.Agente;

/// <summary>Función del agente para consultar la base de conocimiento de la tienda (Qdrant).</summary>
public sealed class ConocimientoPlugin
{
    private const int Fragmentos = 5;

    private readonly IKnowledgeBaseService _conocimiento;
    private readonly SesionAgente _sesion;
    private readonly ILogger<ConocimientoPlugin> _logger;

    public ConocimientoPlugin(
        IKnowledgeBaseService conocimiento,
        SesionAgente sesion,
        ILogger<ConocimientoPlugin> logger)
    {
        _conocimiento = conocimiento;
        _sesion = sesion;
        _logger = logger;
    }

    [KernelFunction("BuscarEnConocimiento")]
    [Description(
        "Busca en la base de conocimiento oficial de la tienda Mundo Mascota: qué es la tienda, qué hace, " +
        "quién la atiende, sus funcionalidades, servicios, políticas, envíos, pagos, horarios y preguntas frecuentes. " +
        "Úsala siempre antes de responder cualquier pregunta sobre la tienda.")]
    public async Task<string> BuscarEnConocimientoAsync(
        [Description("Pregunta o tema a buscar, en español y con palabras clave.")] string consulta,
        CancellationToken cancellationToken = default)
    {
        if (!_conocimiento.EstaConfigurado)
        {
            return "La base de conocimiento de la tienda no está configurada en este momento.";
        }

        if (string.IsNullOrWhiteSpace(consulta))
        {
            return "Indica la pregunta o el tema que se debe buscar.";
        }

        try
        {
            var resultados = await _conocimiento.BuscarAsync(consulta.Trim(), Fragmentos, cancellationToken);
            _sesion.MarcarConocimientoConsultado();

            if (resultados.Count == 0)
            {
                return "No se encontraron fragmentos relevantes en la base de conocimiento de la tienda.";
            }

            var respuesta = new StringBuilder("Fragmentos recuperados de la base de conocimiento de la tienda:");
            foreach (var resultado in resultados)
            {
                _sesion.AgregarFuente(resultado.Nombre);

                respuesta.AppendLine();
                respuesta.AppendLine();
                respuesta.AppendLine($"### {resultado.Nombre} (relevancia {resultado.Puntaje:0.00})");
                respuesta.Append(resultado.Contenido);
            }

            return respuesta.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo consultar la base de conocimiento de la tienda.");
            return "No se pudo consultar la base de conocimiento de la tienda en este momento.";
        }
    }
}
