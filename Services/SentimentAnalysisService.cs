using Microsoft.Extensions.Options;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Text;
using _20262.ML;

namespace _20262.Services;

/// <summary>
/// Entrena y consume el modelo de análisis de sentimiento (ML.NET) sobre comentarios_entrenamiento.csv.
/// El modelo se persiste como .zip en disco y se carga una sola vez por proceso.
/// </summary>
public sealed class SentimentAnalysisService : ISentimentAnalysisService, IDisposable
{
    private const string LabelColumn = "Label";
    private const string FeaturesColumn = "Features";
    private const string ScoreColumn = "Score";
    private const string ProbabilityColumn = "Probability";

    private readonly SentimentOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SentimentAnalysisService> _logger;

    private readonly MLContext _mlContext = new(seed: 42);
    private readonly SemaphoreSlim _cargaLock = new(1, 1);
    private readonly SemaphoreSlim _prediccionLock = new(1, 1);

    private PredictionEngine<SentimentPredictionInput, SentimentPredictionOutput>? _engine;

    public SentimentAnalysisService(
        IOptions<SentimentOptions> options,
        IHostEnvironment environment,
        ILogger<SentimentAnalysisService> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public bool EstaHabilitado => _options.Enabled;

    public bool EstaListo => _engine is not null;

    public MetricasEntrenamientoSentimiento? UltimasMetricas { get; private set; }

    public string ModelPath => ResolverRuta(_options.ModelPath);

    public Task InicializarAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Análisis de sentimiento deshabilitado (Sentimiento:Enabled=false).");
            return Task.CompletedTask;
        }

        return AsegurarModeloAsync(cancellationToken);
    }

    public async Task<ResultadoSentimiento> AnalizarAsync(string texto, CancellationToken cancellationToken = default)
    {
        var resultado = new ResultadoSentimiento { FechaEvaluacion = DateTime.Now };

        if (string.IsNullOrWhiteSpace(texto))
        {
            return resultado;
        }

        if (!_options.Enabled)
        {
            return resultado;
        }

        await AsegurarModeloAsync(cancellationToken);

        var engine = _engine;
        if (engine is null)
        {
            _logger.LogWarning("Análisis de sentimiento omitido: el modelo no está disponible.");
            return resultado;
        }

        await _prediccionLock.WaitAsync(cancellationToken);
        try
        {
            var prediccion = engine.Predict(new SentimentPredictionInput { Comentario = texto });

            resultado.Analizado = true;
            resultado.EsPositivo = prediccion.Label;
            resultado.Probabilidad = prediccion.Label ? prediccion.Probability : 1 - prediccion.Probability;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al predecir el sentimiento de un mensaje de contacto.");
        }
        finally
        {
            _prediccionLock.Release();
        }

        return resultado;
    }

    public async Task<bool> ReentrenarAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Análisis de sentimiento deshabilitado (Sentimiento:Enabled=false).");
            return false;
        }

        await _cargaLock.WaitAsync(cancellationToken);
        try
        {
            return await EntrenarYGuardarAsync(cancellationToken);
        }
        finally
        {
            _cargaLock.Release();
        }
    }

    private async Task AsegurarModeloAsync(CancellationToken cancellationToken)
    {
        if (_engine is not null)
        {
            return;
        }

        await _cargaLock.WaitAsync(cancellationToken);
        try
        {
            if (_engine is not null)
            {
                return;
            }

            if (File.Exists(ModelPath))
            {
                CargarModelo(ModelPath);
                return;
            }

            if (!_options.TrainOnStartup)
            {
                _logger.LogWarning(
                    "No existe el modelo de sentimiento en '{ModelPath}' y Sentimiento:TrainOnStartup=false.",
                    ModelPath);
                return;
            }

            await EntrenarYGuardarAsync(cancellationToken);
        }
        finally
        {
            _cargaLock.Release();
        }
    }

    private async Task<bool> EntrenarYGuardarAsync(CancellationToken cancellationToken)
    {
        var dataPath = ResolverRuta(_options.TrainingDataPath);

        if (!File.Exists(dataPath))
        {
            _logger.LogError(
                "No se encontró el dataset de entrenamiento '{DataPath}'; el análisis de sentimiento queda deshabilitado.",
                dataPath);
            return false;
        }

        try
        {
            _logger.LogInformation("Entrenando modelo de sentimiento con '{DataPath}'...", dataPath);

            var datos = _mlContext.Data.LoadFromTextFile<SentimentTrainingData>(
                dataPath,
                separatorChar: ',',
                hasHeader: true,
                allowQuoting: true);

            var metricas = ContarEjemplos(datos);

            var pipeline = _mlContext.Transforms.Text
                .FeaturizeText(FeaturesColumn, nameof(SentimentTrainingData.Comentario))
                .Append(_mlContext.BinaryClassification.Trainers
                    .LbfgsLogisticRegression(LabelColumn, FeaturesColumn));

            if (metricas.EjemplosTotales >= 20)
            {
                var split = _mlContext.Data.TrainTestSplit(datos, testFraction: 0.2, seed: 42);
                var modeloValidacion = pipeline.Fit(split.TrainSet);
                var evaluacion = _mlContext.BinaryClassification.Evaluate(
                    modeloValidacion.Transform(split.TestSet),
                    labelColumnName: LabelColumn,
                    scoreColumnName: ScoreColumn,
                    probabilityColumnName: ProbabilityColumn);

                metricas.EjemplosEntrenamiento = (int)(split.TrainSet.GetRowCount() ?? 0);
                metricas.EjemplosPrueba = (int)(split.TestSet.GetRowCount() ?? 0);
                metricas.Accuracy = evaluacion.Accuracy;
                metricas.LogLoss = evaluacion.LogLoss;
                metricas.Entropy = evaluacion.Entropy;
            }

            var modelo = pipeline.Fit(datos);

            var directorio = Path.GetDirectoryName(ModelPath);
            if (!string.IsNullOrEmpty(directorio))
            {
                Directory.CreateDirectory(directorio);
            }

            var esquemaPrediccion = _mlContext.Data
                .LoadFromEnumerable(Array.Empty<SentimentPredictionInput>()).Schema;
            _mlContext.Model.Save(modelo, esquemaPrediccion, ModelPath);
            CargarModelo(ModelPath);

            UltimasMetricas = metricas;

            _logger.LogInformation(
                "Modelo de sentimiento entrenado: {Total} ejemplos ({Positivos} positivos / {Negativos} negativos), accuracy {Accuracy:P2}, logloss {LogLoss}. Guardado en '{ModelPath}'.",
                metricas.EjemplosTotales,
                metricas.Positivos,
                metricas.Negativos,
                metricas.Accuracy,
                metricas.LogLoss,
                ModelPath);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al entrenar el modelo de sentimiento con '{DataPath}'.", dataPath);
            return false;
        }
    }

    private MetricasEntrenamientoSentimiento ContarEjemplos(IDataView datos)
    {
        var metricas = new MetricasEntrenamientoSentimiento();

        foreach (var fila in _mlContext.Data.CreateEnumerable<SentimentTrainingData>(datos, reuseRowObject: false))
        {
            metricas.EjemplosTotales++;
            if (fila.Label)
            {
                metricas.Positivos++;
            }
            else
            {
                metricas.Negativos++;
            }
        }

        return metricas;
    }

    private void CargarModelo(string modelPath)
    {
        try
        {
            var modelo = _mlContext.Model.Load(modelPath, out var esquemaEntrada);
            _engine = _mlContext.Model.CreatePredictionEngine<SentimentPredictionInput, SentimentPredictionOutput>(
                modelo,
                esquemaEntrada);

            _logger.LogInformation("Modelo de sentimiento cargado desde '{ModelPath}'.", modelPath);
        }
        catch (Exception ex)
        {
            _engine = null;
            _logger.LogError(ex, "No se pudo cargar el modelo de sentimiento desde '{ModelPath}'.", modelPath);
        }
    }

    private string ResolverRuta(string ruta) =>
        Path.IsPathRooted(ruta) ? ruta : Path.Combine(_environment.ContentRootPath, ruta);

    public void Dispose()
    {
        _engine?.Dispose();
        _cargaLock.Dispose();
        _prediccionLock.Dispose();
    }
}
