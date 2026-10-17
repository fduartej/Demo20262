namespace _20262.Integrations.Qdrant;

/// <summary>Documento de conocimiento indexado en Qdrant, agrupado por carga.</summary>
public class DocumentoConocimiento
{
    public string Id { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string Tipo { get; set; } = string.Empty;

    public int Fragmentos { get; set; }

    public int Caracteres { get; set; }

    public DateTime FechaCarga { get; set; }
}

/// <summary>Resultado de una búsqueda semántica sobre la base de conocimiento.</summary>
public class ResultadoConocimiento
{
    public string Nombre { get; set; } = string.Empty;

    public string Contenido { get; set; } = string.Empty;

    public double Puntaje { get; set; }
}
