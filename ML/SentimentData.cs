using Microsoft.ML.Data;

namespace _20262.ML;

/// <summary>
/// Fila del dataset de entrenamiento comentarios_entrenamiento.csv (columnas Comentario, Label).
/// ML.NET mapea las columnas por nombre usando el encabezado del CSV.
/// Label = true significa sentimiento positivo, false significa negativo.
/// </summary>
public class SentimentTrainingData
{
    [LoadColumn(0)]
    public string Comentario { get; set; } = string.Empty;

    [LoadColumn(1)]
    public bool Label { get; set; }
}
