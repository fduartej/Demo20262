using Microsoft.ML.Data;

namespace _20262.ML;

/// <summary>
/// Texto libre que se envía al modelo para clasificar. La propiedad se llama igual que la columna
/// "Comentario" del dataset porque ML.NET resuelve las columnas por nombre.
/// </summary>
public class SentimentPredictionInput
{
    public string Comentario { get; set; } = string.Empty;
}

/// <summary>Salida del clasificador binario: etiqueta predicha y probabilidad de la clase predicha.</summary>
public class SentimentPredictionOutput
{
    [ColumnName("PredictedLabel")]
    public bool Label { get; set; }

    [ColumnName("Probability")]
    public float Probability { get; set; }
}

/// <summary>Resultado de sentimiento listo para persistir y mostrar en el reporte del administrador.</summary>
public class ResultadoSentimiento
{
    public const string EtiquetaPositivo = "Positivo";
    public const string EtiquetaNegativo = "Negativo";

    /// <summary>True si el mensaje fue clasificado; false si el modelo no estaba disponible o el texto venía vacío.</summary>
    public bool Analizado { get; set; }

    public bool EsPositivo { get; set; }

    /// <summary>Probabilidad (0-1) de la clase predicha.</summary>
    public double Probabilidad { get; set; }

    public string? Etiqueta => Analizado
        ? (EsPositivo ? EtiquetaPositivo : EtiquetaNegativo)
        : null;

    public DateTime FechaEvaluacion { get; set; } = DateTime.Now;
}

/// <summary>Métricas de la última corrida de entrenamiento, para el log y la pantalla del administrador.</summary>
public class MetricasEntrenamientoSentimiento
{
    public int EjemplosTotales { get; set; }

    public int EjemplosEntrenamiento { get; set; }

    public int EjemplosPrueba { get; set; }

    public int Positivos { get; set; }

    public int Negativos { get; set; }

    public double Accuracy { get; set; }

    public double? LogLoss { get; set; }

    public double? Entropy { get; set; }

    public DateTime FechaEntrenamiento { get; set; } = DateTime.Now;
}
