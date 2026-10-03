namespace _20262.Services;

/// <summary>
/// Configuración del modelo de análisis de sentimiento (ML.NET) usado en los mensajes de contacto.
/// Rutas relativas al content root de la aplicación.
/// </summary>
public class SentimentOptions
{
    public const string SectionName = "Sentimiento";

    /// <summary>Activa el análisis de sentimiento (si es false no se entrena ni se predice).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Datos de entrenamiento con columnas Comentario,Label.</summary>
    public string TrainingDataPath { get; set; } = "ML/sentimenalAnalysisNLP/trainingdata/comentarios_entrenamiento.csv";

    /// <summary>Archivo .zip generado por el entrenamiento (artefacto, no se versiona).</summary>
    public string ModelPath { get; set; } = "ML/sentimenalAnalysisNLP/model/modeloSentimiento.zip";

    /// <summary>Si el modelo no existe en disco, se entrena al arrancar la aplicación.</summary>
    public bool TrainOnStartup { get; set; } = true;

    /// <summary>Reentrena el modelo aunque ya exista en disco (útil al cambiar el dataset).</summary>
    public bool RetrainOnStartup { get; set; }
}
