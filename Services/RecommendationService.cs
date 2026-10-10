using Microsoft.Extensions.Options;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Recommender;
using Microsoft.ML.Trainers;
using _20262.ML;

namespace _20262.Services;

/// <summary>
/// Entrena y consume el modelo de recomendación por factorización de matrices (ML.NET) sobre
/// product_ratings_simple_10k.csv. Rating es el label, userId la fila y productId la columna de la matriz.
/// El modelo se persiste como .zip en disco y se carga una sola vez por proceso.
/// </summary>
public sealed class RecommendationService : IRecommendationService, IDisposable
{
    private const string ScoreColumn = "Score";
    private const string LabelColumn = nameof(RatingTrainingData.Rating);

    private readonly RecommendationOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<RecommendationService> _logger;

    private readonly MLContext _mlContext = new(seed: 42);
    private readonly SemaphoreSlim _cargaLock = new(1, 1);
    private readonly SemaphoreSlim _prediccionLock = new(1, 1);

    private PredictionEngine<RatingTrainingData, RatingPrediction>? _engine;

    // Vocabulario aprendido del dataset para servir predicciones y el arranque en frío.
    private HashSet<string> _usuariosConocidos = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<int> _productosConocidos = [];
    private Dictionary<int, float> _popularidadPorProducto = [];
    private int _cantidadProductos;

    public RecommendationService(
        IOptions<RecommendationOptions> options,
        IHostEnvironment environment,
        ILogger<RecommendationService> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public bool EstaHabilitado => _options.Enabled;

    public bool EstaListo => _engine is not null;

    public MetricasEntrenamientoRecomendacion? UltimasMetricas { get; private set; }

    public string ModelPath => ResolverRuta(_options.ModelPath);

    public bool EsUsuarioConocido(string userId)
        => !string.IsNullOrWhiteSpace(userId) && _usuariosConocidos.Contains(userId);

    public Task InicializarAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Modelo de recomendación deshabilitado (Recomendacion:Enabled=false).");
            return Task.CompletedTask;
        }

        return AsegurarModeloAsync(cancellationToken);
    }

    public async Task<List<RecomendacionProducto>> RecomendarAsync(
        string userId,
        IEnumerable<int> candidatos,
        int topK,
        CancellationToken cancellationToken = default)
    {
        var resultado = new List<RecomendacionProducto>();

        if (!_options.Enabled || string.IsNullOrWhiteSpace(userId))
        {
            return resultado;
        }

        await AsegurarModeloAsync(cancellationToken);

        var engine = _engine;
        if (engine is null)
        {
            _logger.LogWarning("Recomendación omitida: el modelo no está disponible.");
            return resultado;
        }

        // Solo productos que existen en el catálogo y fueron vistos en el entrenamiento.
        var objetivo = _productosConocidos
            .Intersect(candidatos.Distinct())
            .OrderBy(id => id)
            .ToList();

        await _prediccionLock.WaitAsync(cancellationToken);
        try
        {
            if (EsUsuarioConocido(userId) && objetivo.Count > 0)
            {
                foreach (var productId in objetivo)
                {
                    var prediccion = engine.Predict(new RatingTrainingData
                    {
                        UserId = userId,
                        ProductId = productId,
                        Rating = 0
                    });
                    resultado.Add(new RecomendacionProducto { ProductId = productId, Score = prediccion.Score });
                }

                resultado = resultado
                    .OrderByDescending(r => r.Score)
                    .Take(Math.Max(0, topK))
                    .ToList();
            }
            else
            {
                // Arranque en frío: usuarios sin historial reciben los productos mejor valorados.
                var objetivoPopularidad = _popularidadPorProducto
                    .Where(kvp => candidatos.Contains(kvp.Key))
                    .OrderByDescending(kvp => kvp.Value)
                    .ThenBy(kvp => kvp.Key)
                    .Take(Math.Max(0, topK));

                resultado = objetivoPopularidad
                    .Select(kvp => new RecomendacionProducto { ProductId = kvp.Key, Score = kvp.Value, DeFrio = true })
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al recomendar productos para el usuario '{UserId}'.", userId);
            resultado = [];
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
            _logger.LogWarning("Modelo de recomendación deshabilitado (Recomendacion:Enabled=false).");
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

            // Recomendacion:RetrainOnStartup fuerza el entrenamiento aunque exista el .zip en disco.
            if (_options.RetrainOnStartup)
            {
                await EntrenarYGuardarAsync(cancellationToken);
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
                    "No existe el modelo de recomendación en '{ModelPath}' y Recomendacion:TrainOnStartup=false.",
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
                "No se encontró el dataset de entrenamiento '{DataPath}'; el modelo de recomendación queda deshabilitado.",
                dataPath);
            return false;
        }

        try
        {
            _logger.LogInformation("Entrenando modelo de recomendación con '{DataPath}'...", dataPath);

            var datos = _mlContext.Data.LoadFromTextFile<RatingTrainingData>(
                dataPath,
                separatorChar: ',',
                hasHeader: true,
                allowQuoting: true);

            cancellationToken.ThrowIfCancellationRequested();

            CargarVocabulario(datos);

            var userIdKey = nameof(RatingTrainingData.UserId) + "Key";
            var productIdKey = nameof(RatingTrainingData.ProductId) + "Key";

            var pipeline = _mlContext.Transforms.Conversion.MapValueToKey(
                    outputColumnName: userIdKey,
                    inputColumnName: nameof(RatingTrainingData.UserId))
                .Append(_mlContext.Transforms.Conversion.MapValueToKey(
                    outputColumnName: productIdKey,
                    inputColumnName: nameof(RatingTrainingData.ProductId)))
                .Append(_mlContext.Recommendation().Trainers.MatrixFactorization(
                    new MatrixFactorizationTrainer.Options
                    {
                        MatrixColumnIndexColumnName = userIdKey,
                        MatrixRowIndexColumnName = productIdKey,
                        LabelColumnName = LabelColumn,
                        NumberOfIterations = _options.NumberOfIterations,
                        ApproximationRank = _options.ApproximationRank,
                        LearningRate = (float)_options.LearningRate
                    }));

            var metricas = new MetricasEntrenamientoRecomendacion
            {
                EjemplosTotales = (int)(datos.GetRowCount() ?? 0),
                EjemplosEntrenamiento = (int)(datos.GetRowCount() ?? 0),
                Usuarios = _usuariosConocidos.Count,
                Productos = _cantidadProductos
            };

            var modelo = pipeline.Fit(datos);

            var evaluacion = _mlContext.Regression.Evaluate(
                modelo.Transform(datos),
                labelColumnName: LabelColumn,
                scoreColumnName: ScoreColumn);
            metricas.Rms = evaluacion.RootMeanSquaredError;
            metricas.R2 = evaluacion.RSquared;

            var directorio = Path.GetDirectoryName(ModelPath);
            if (!string.IsNullOrEmpty(directorio))
            {
                Directory.CreateDirectory(directorio);
            }

            _mlContext.Model.Save(modelo, datos.Schema, ModelPath);
            CargarModelo(ModelPath);

            UltimasMetricas = metricas;

            _logger.LogInformation(
                "Modelo de recomendación entrenado: {Total} ratings, {Usuarios} usuarios, {Productos} productos, RMSE {Rms:F4}, R² {R2:F4}. Guardado en '{ModelPath}'.",
                metricas.EjemplosTotales,
                metricas.Usuarios,
                metricas.Productos,
                metricas.Rms,
                metricas.R2.GetValueOrDefault(),
                ModelPath);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al entrenar el modelo de recomendación con '{DataPath}'.", dataPath);
            return false;
        }
    }

    /// <summary>
    /// Recorre el dataset para recordar qué usuarios y productos conoce el modelo y el promedio de
    /// calificación por producto (usado como arranque en frío para usuarios sin historial).
    /// </summary>
    private void CargarVocabulario(IDataView datos)
    {
        var usuarios = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var productos = new HashSet<int>();
        var sumaRatings = new Dictionary<int, double>();
        var conteoRatings = new Dictionary<int, int>();

        foreach (var fila in _mlContext.Data.CreateEnumerable<RatingTrainingData>(datos, reuseRowObject: false))
        {
            if (string.IsNullOrWhiteSpace(fila.UserId) || fila.ProductId <= 0)
            {
                continue;
            }

            usuarios.Add(fila.UserId);

            var productId = (int)fila.ProductId;
            productos.Add(productId);

            sumaRatings[productId] = sumaRatings.GetValueOrDefault(productId) + fila.Rating;
            conteoRatings[productId] = conteoRatings.GetValueOrDefault(productId) + 1;
        }

        _usuariosConocidos = usuarios;
        _productosConocidos = productos;
        _cantidadProductos = productos.Count;

        _popularidadPorProducto = productos.ToDictionary(
            id => id,
            id => (float)(sumaRatings.GetValueOrDefault(id) / conteoRatings.GetValueOrDefault(id)));
    }

    private void CargarModelo(string modelPath)
    {
        try
        {
            var modelo = _mlContext.Model.Load(modelPath, out var esquemaEntrada);
            _engine = _mlContext.Model.CreatePredictionEngine<RatingTrainingData, RatingPrediction>(
                modelo,
                esquemaEntrada);

            _logger.LogInformation("Modelo de recomendación cargado desde '{ModelPath}'.", modelPath);
        }
        catch (Exception ex)
        {
            _engine = null;
            _logger.LogError(ex, "No se pudo cargar el modelo de recomendación desde '{ModelPath}'.", modelPath);
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