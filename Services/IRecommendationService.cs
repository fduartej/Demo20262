using _20262.ML;

namespace _20262.Services;

public interface IRecommendationService
{
    bool EstaHabilitado { get; }

    /// <summary>True cuando el modelo está cargado y listo para predecir.</summary>
    bool EstaListo { get; }

    MetricasEntrenamientoRecomendacion? UltimasMetricas { get; }

    string ModelPath { get; }

    /// <summary>True si el usuario (Guid de Identity) aparece en el dataset de ratings y el modelo puede predecir sus gustos.</summary>
    bool EsUsuarioConocido(string userId);

    /// <summary>Carga el modelo entrenado; si no existe en disco, lo entrena desde el dataset configurado.</summary>
    Task InicializarAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve los <paramref name="topK"/> productos recomendados para el usuario.
    /// Solo considera los <paramref name="candidatos"/> (ids de productos del catálogo) que existen en el dataset.
    /// Si el usuario no está en el dataset, cae a los mejor valorados (popularidad).
    /// </summary>
    Task<List<RecomendacionProducto>> RecomendarAsync(
        string userId,
        IEnumerable<int> candidatos,
        int topK,
        CancellationToken cancellationToken = default);

    /// <summary>Reentrena el modelo con el dataset configurado y lo persiste en disco.</summary>
    Task<bool> ReentrenarAsync(CancellationToken cancellationToken = default);
}