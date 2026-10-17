namespace _20262.Integrations.Qdrant;

public interface IKnowledgeBaseService
{
    bool EstaConfigurado { get; }

    Task<int> CargarDocumentoAsync(
        string nombreArchivo,
        string tipo,
        string contenido,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentoConocimiento>> ObtenerDocumentosAsync(
        string? busqueda = null,
        CancellationToken cancellationToken = default);

    Task<int> EliminarDocumentoAsync(
        string documentoId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResultadoConocimiento>> BuscarAsync(
        string consulta,
        int limite = 5,
        CancellationToken cancellationToken = default);
}
