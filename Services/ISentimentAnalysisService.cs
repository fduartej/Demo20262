using _20262.ML;

namespace _20262.Services;

public interface ISentimentAnalysisService
{
    bool EstaHabilitado { get; }

    /// <summary>True cuando el modelo está cargado y listo para predecir.</summary>
    bool EstaListo { get; }

    MetricasEntrenamientoSentimiento? UltimasMetricas { get; }

    string ModelPath { get; }

    /// <summary>Carga el modelo entrenado; si no existe en disco, lo entrena desde el dataset configurado.</summary>
    Task InicializarAsync(CancellationToken cancellationToken = default);

    /// <summary>Clasifica el texto como positivo o negativo. Nunca lanza excepciones: si el modelo no está disponible devuelve un resultado sin clasificar.</summary>
    Task<ResultadoSentimiento> AnalizarAsync(string texto, CancellationToken cancellationToken = default);

    /// <summary>Reentrena el modelo con el dataset configurado y lo persiste en disco.</summary>
    Task<bool> ReentrenarAsync(CancellationToken cancellationToken = default);
}
