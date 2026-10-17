using _20262.Integrations.Qdrant;

namespace _20262.Models.ViewModels;

/// <summary>Panel de administración para cargar documentos a la base de conocimiento (Qdrant).</summary>
public class ConocimientoViewModel
{
    public bool QdrantConfigurado { get; set; }

    public string QdrantEndpoint { get; set; } = string.Empty;

    public string Coleccion { get; set; } = string.Empty;

    public string ModeloEmbedding { get; set; } = string.Empty;

    public bool GroqConfigurado { get; set; }

    public string GroqModelo { get; set; } = string.Empty;

    public List<DocumentoConocimiento> Documentos { get; set; } = [];

    public string? Busqueda { get; set; }

    public int Pagina { get; set; } = 1;

    public int TamanoPagina { get; set; } = 10;

    public string? Consulta { get; set; }

    public List<ResultadoConocimiento>? Resultados { get; set; }

    public int TotalDocumentos => Documentos.Count;

    public int TotalFragmentos => Documentos.Sum(d => d.Fragmentos);

    public int TotalPaginas => TotalDocumentos == 0
        ? 1
        : (int)Math.Ceiling(TotalDocumentos / (double)Math.Max(TamanoPagina, 1));

    public IEnumerable<DocumentoConocimiento> DocumentosPagina => Documentos
        .Skip((Math.Max(Pagina, 1) - 1) * TamanoPagina)
        .Take(TamanoPagina);
}
