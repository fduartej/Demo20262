using Microsoft.ML.Data;

namespace _20262.ML;

/// <summary>
/// Fila del dataset product_ratings_simple_10k.csv (columnas userId, productId, rating).
/// ML.NET mapea las columnas por nombre usando el encabezado del CSV.
/// Rating es el label que la factorización de matrices intenta predecir.
/// </summary>
public class RatingTrainingData
{
    [LoadColumn(0)]
    public string UserId { get; set; } = string.Empty;

    [LoadColumn(1)]
    public float ProductId { get; set; }

    [LoadColumn(2)]
    public float Rating { get; set; }
}

/// <summary>Salida del predictor de factorización de matrices: la columna "Score" con la calificación predicha.</summary>
public class RatingPrediction
{
    [ColumnName("Score")]
    public float Score { get; set; }
}

/// <summary>Producto recomendado por el modelo (por relación aprendida o por popularidad en frío).</summary>
public class RecomendacionProducto
{
    public int ProductId { get; set; }

    /// <summary>Calificación predicha (1-5) o promedio de popularidad cuando <see cref="DeFrio"/> es true.</summary>
    public float Score { get; set; }

    /// <summary>True cuando se usó la heurística de popularidad porque el usuario no está en el dataset de ratings.</summary>
    public bool DeFrio { get; set; }
}

/// <summary>Métricas de la última corrida de entrenamiento, para el log y la pantalla de recomendaciones.</summary>
public class MetricasEntrenamientoRecomendacion
{
    public int EjemplosTotales { get; set; }

    public int EjemplosEntrenamiento { get; set; }

    public int EjemplosPrueba { get; set; }

    public int Usuarios { get; set; }

    public int Productos { get; set; }

    /// <summary>Root Mean Squared Error sobre el dataset de entrenamiento (0 es perfecto).</summary>
    public double Rms { get; set; }

    /// <summary>Coeficiente de determinación R² (1 es perfecto).</summary>
    public double? R2 { get; set; }

    public DateTime FechaEntrenamiento { get; set; } = DateTime.Now;
}