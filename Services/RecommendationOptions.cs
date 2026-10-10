namespace _20262.Services;

/// <summary>
/// Configuración del modelo de recomendación por factorización de matrices (ML.NET).
/// Rutas relativas al content root de la aplicación.
/// </summary>
public class RecommendationOptions
{
    public const string SectionName = "Recomendacion";

    /// <summary>Activa el modelo de recomendación (si es false no se entrena ni se predice).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Dataset de entrenamiento con columnas userId, productId, rating.</summary>
    public string TrainingDataPath { get; set; } = "ML/recomendacionProductos/trainingdata/product_ratings_simple_10k.csv";

    /// <summary>Archivo .zip generado por el entrenamiento (artefacto, no se versiona).</summary>
    public string ModelPath { get; set; } = "ML/recomendacionProductos/model/modeloRecomendacion.zip";

    /// <summary>Si el modelo no existe en disco, se entrena al arrancar la aplicación.</summary>
    public bool TrainOnStartup { get; set; } = true;

    /// <summary>Reentrena el modelo aunque ya exista en disco (útil al cambiar el dataset).</summary>
    public bool RetrainOnStartup { get; set; }

    /// <summary>Cantidad de productos recomendados a mostrar al usuario.</summary>
    public int TopK { get; set; } = 6;

    /// <summary>Iteraciones del algoritmo ALS de factorización de matrices.</summary>
    public int NumberOfIterations { get; set; } = 20;

    /// <summary>Rango de aproximación (dimensiones latentes) de la factorización.</summary>
    public int ApproximationRank { get; set; } = 100;

    /// <summary>Tasa de aprendizaje del entrenamiento.</summary>
    public double LearningRate { get; set; } = 0.05;
}