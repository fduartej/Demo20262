using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace _20262.Integrations.Qdrant;

public class QdrantKnowledgeService : IKnowledgeBaseService
{
    private const int MaxCaracteresFragmento = 1200;
    private const int SolapeCaracteres = 200;
    private const int PuntosPorLote = 64;
    private const string NombreVector = "text";

    private readonly HttpClient _http;
    private readonly QdrantOptions _options;
    private readonly ILogger<QdrantKnowledgeService> _logger;

    public QdrantKnowledgeService(
        HttpClient http,
        IOptions<QdrantOptions> options,
        ILogger<QdrantKnowledgeService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        if (_options.EstaConfigurado)
        {
            _http.BaseAddress = new Uri(_options.Endpoint.TrimEnd('/') + "/");
            _http.DefaultRequestHeaders.TryAddWithoutValidation("api-key", _options.ApiKey);
        }
    }

    public bool EstaConfigurado => _options.EstaConfigurado;

    public async Task<int> CargarDocumentoAsync(
        string nombreArchivo,
        string tipo,
        string contenido,
        CancellationToken cancellationToken = default)
    {
        AsegurarConfigurado();
        await AsegurarColeccionAsync(cancellationToken);

        var fragmentos = Fragmentar(contenido);
        if (fragmentos.Count == 0)
        {
            return 0;
        }

        var documentoId = Guid.NewGuid().ToString();
        var fechaCarga = DateTime.UtcNow;

        var puntos = fragmentos.Select((texto, indice) => new
        {
            id = Guid.NewGuid().ToString(),
            vector = new Dictionary<string, object>
            {
                [NombreVector] = new { text = texto, model = _options.EmbeddingModel }
            },
            payload = new
            {
                documentoId,
                documento = nombreArchivo,
                tipo,
                fragmento = indice,
                totalFragmentos = fragmentos.Count,
                contenido = texto,
                caracteres = texto.Length,
                fechaCarga
            }
        }).ToList();

        foreach (var lote in puntos.Chunk(PuntosPorLote))
        {
            var respuesta = await _http.PutAsJsonAsync(
                $"collections/{_options.CollectionName}/points?wait=true",
                new { points = lote },
                cancellationToken);

            await AsegurarExitoAsync(respuesta, "cargar fragmentos en Qdrant", cancellationToken);
        }

        _logger.LogInformation(
            "Qdrant | Documento '{Documento}' ({Tipo}) indexado: {Fragmentos} fragmentos, {Caracteres} caracteres.",
            nombreArchivo, tipo, fragmentos.Count, contenido.Length);

        return fragmentos.Count;
    }

    public async Task<IReadOnlyList<DocumentoConocimiento>> ObtenerDocumentosAsync(
        string? busqueda = null,
        CancellationToken cancellationToken = default)
    {
        if (!EstaConfigurado)
        {
            return [];
        }

        var documentos = new Dictionary<string, DocumentoConocimiento>(StringComparer.Ordinal);
        string? offset = null;

        do
        {
            var cuerpo = new Dictionary<string, object>
            {
                ["limit"] = 256,
                ["with_payload"] = true,
                ["with_vector"] = false
            };
            if (offset is not null)
            {
                cuerpo["offset"] = offset;
            }

            var respuesta = await _http.PostAsJsonAsync(
                $"collections/{_options.CollectionName}/points/scroll", cuerpo, cancellationToken);

            if (respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                return [];
            }

            await AsegurarExitoAsync(respuesta, "listar documentos de Qdrant", cancellationToken);

            using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(cancellationToken));
            var resultado = documento.RootElement.GetProperty("result");

            foreach (var punto in resultado.GetProperty("points").EnumerateArray())
            {
                if (!punto.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var id = LeerTexto(payload, "documentoId");
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                if (!documentos.TryGetValue(id, out var entrada))
                {
                    entrada = new DocumentoConocimiento
                    {
                        Id = id,
                        Nombre = LeerTexto(payload, "documento"),
                        Tipo = LeerTexto(payload, "tipo"),
                        FechaCarga = LeerFecha(payload, "fechaCarga")
                    };
                    documentos[id] = entrada;
                }

                entrada.Fragmentos++;
                entrada.Caracteres += payload.TryGetProperty("caracteres", out var c) && c.TryGetInt32(out var caracteres)
                    ? caracteres
                    : 0;
            }

            offset = resultado.TryGetProperty("next_page_offset", out var siguiente) && siguiente.ValueKind == JsonValueKind.String
                ? siguiente.GetString()
                : null;
        }
        while (!string.IsNullOrEmpty(offset));

        IEnumerable<DocumentoConocimiento> lista = documentos.Values;

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var termino = busqueda.Trim();
            lista = lista.Where(d => d.Nombre.Contains(termino, StringComparison.OrdinalIgnoreCase));
        }

        return lista
            .OrderByDescending(d => d.FechaCarga)
            .ThenBy(d => d.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<int> EliminarDocumentoAsync(
        string documentoId,
        CancellationToken cancellationToken = default)
    {
        AsegurarConfigurado();

        var filtro = new
        {
            must = new[]
            {
                new { key = "documentoId", match = new { value = documentoId } }
            }
        };

        var conteo = await _http.PostAsJsonAsync(
            $"collections/{_options.CollectionName}/points/count",
            new { filter = filtro, exact = true },
            cancellationToken);

        if (conteo.StatusCode == HttpStatusCode.NotFound)
        {
            return 0;
        }

        await AsegurarExitoAsync(conteo, "contar fragmentos del documento", cancellationToken);

        var total = 0;
        using (var documento = JsonDocument.Parse(await conteo.Content.ReadAsStringAsync(cancellationToken)))
        {
            if (documento.RootElement.TryGetProperty("result", out var resultado) &&
                resultado.TryGetProperty("count", out var contador) &&
                contador.TryGetInt32(out var valor))
            {
                total = valor;
            }
        }

        var respuesta = await _http.PostAsJsonAsync(
            $"collections/{_options.CollectionName}/points/delete?wait=true",
            new { filter = filtro },
            cancellationToken);

        await AsegurarExitoAsync(respuesta, "eliminar documento de Qdrant", cancellationToken);

        _logger.LogInformation("Qdrant | Documento {DocumentoId} eliminado: {Fragmentos} fragmentos.", documentoId, total);
        return total;
    }

    public async Task<IReadOnlyList<ResultadoConocimiento>> BuscarAsync(
        string consulta,
        int limite = 5,
        CancellationToken cancellationToken = default)
    {
        if (!EstaConfigurado || string.IsNullOrWhiteSpace(consulta))
        {
            return [];
        }

        var cuerpo = new
        {
            query = new { text = consulta, model = _options.EmbeddingModel },
            @using = NombreVector,
            limit = limite,
            with_payload = true
        };

        var respuesta = await _http.PostAsJsonAsync(
            $"collections/{_options.CollectionName}/points/query", cuerpo, cancellationToken);

        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        await AsegurarExitoAsync(respuesta, "consultar la base de conocimiento", cancellationToken);

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(cancellationToken));
        var puntos = documento.RootElement.GetProperty("result").GetProperty("points");

        var resultados = new List<ResultadoConocimiento>();
        foreach (var punto in puntos.EnumerateArray())
        {
            var nombre = string.Empty;
            var contenido = string.Empty;

            if (punto.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object)
            {
                nombre = LeerTexto(payload, "documento");
                contenido = LeerTexto(payload, "contenido");
            }

            resultados.Add(new ResultadoConocimiento
            {
                Nombre = nombre,
                Contenido = contenido,
                Puntaje = punto.TryGetProperty("score", out var score) ? score.GetDouble() : 0
            });
        }

        return resultados;
    }

    private async Task AsegurarColeccionAsync(CancellationToken cancellationToken)
    {
        var respuesta = await _http.GetAsync($"collections/{_options.CollectionName}", cancellationToken);
        if (respuesta.IsSuccessStatusCode)
        {
            await AsegurarIndicesAsync(cancellationToken);
            return;
        }

        if (respuesta.StatusCode != HttpStatusCode.NotFound)
        {
            await AsegurarExitoAsync(respuesta, "verificar la colección de Qdrant", cancellationToken);
        }

        var cuerpo = new
        {
            vectors = new Dictionary<string, object>
            {
                [NombreVector] = new { size = _options.VectorSize, distance = "Cosine" }
            }
        };

        var creacion = await _http.PutAsJsonAsync($"collections/{_options.CollectionName}", cuerpo, cancellationToken);
        await AsegurarExitoAsync(creacion, "crear la colección de Qdrant", cancellationToken);

        _logger.LogInformation(
            "Qdrant | Colección '{Coleccion}' creada (modelo {Modelo}, {Dimensiones} dimensiones).",
            _options.CollectionName, _options.EmbeddingModel, _options.VectorSize);

        await AsegurarIndicesAsync(cancellationToken);
    }

    private async Task AsegurarIndicesAsync(CancellationToken cancellationToken)
    {
        foreach (var campo in new[] { "documentoId", "documento" })
        {
            var respuesta = await _http.PutAsJsonAsync(
                $"collections/{_options.CollectionName}/index",
                new { field_name = campo, field_schema = "keyword" },
                cancellationToken);

            if (!respuesta.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "Qdrant | Índice de '{Campo}' no creado ({Codigo}); probablemente ya existe.",
                    campo, (int)respuesta.StatusCode);
            }
        }
    }

    private static List<string> Fragmentar(string contenido)
    {
        var texto = (contenido ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        if (texto.Length == 0)
        {
            return [];
        }

        var fragmentos = new List<string>();
        var actual = new StringBuilder();

        foreach (var bloque in texto.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var parrafo = bloque.Trim();
            if (parrafo.Length == 0)
            {
                continue;
            }

            if (parrafo.Length > MaxCaracteresFragmento)
            {
                if (actual.Length > 0)
                {
                    fragmentos.Add(actual.ToString().Trim());
                    actual.Clear();
                }

                fragmentos.AddRange(DividirParrafoLargo(parrafo));
                continue;
            }

            if (actual.Length > 0 && actual.Length + parrafo.Length + 2 > MaxCaracteresFragmento)
            {
                fragmentos.Add(actual.ToString().Trim());
                actual.Clear();
            }

            if (actual.Length > 0)
            {
                actual.Append("\n\n");
            }

            actual.Append(parrafo);
        }

        if (actual.Length > 0)
        {
            fragmentos.Add(actual.ToString().Trim());
        }

        return fragmentos;
    }

    private static IEnumerable<string> DividirParrafoLargo(string texto)
    {
        var inicio = 0;
        while (inicio < texto.Length)
        {
            var largo = Math.Min(MaxCaracteresFragmento, texto.Length - inicio);
            yield return texto.Substring(inicio, largo).Trim();

            if (inicio + largo >= texto.Length)
            {
                break;
            }

            inicio += MaxCaracteresFragmento - SolapeCaracteres;
        }
    }

    private static string LeerTexto(JsonElement payload, string propiedad) =>
        payload.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString() ?? string.Empty
            : string.Empty;

    private static DateTime LeerFecha(JsonElement payload, string propiedad) =>
        payload.TryGetProperty(propiedad, out var valor) &&
        valor.ValueKind == JsonValueKind.String &&
        valor.TryGetDateTime(out var fecha)
            ? fecha.ToLocalTime()
            : default;

    private void AsegurarConfigurado()
    {
        if (!EstaConfigurado)
        {
            throw new InvalidOperationException(
                "Qdrant no está configurado. Completa Endpoint, ApiKey y CollectionName en la sección Qdrant de appsettings.json.");
        }
    }

    private async Task AsegurarExitoAsync(HttpResponseMessage respuesta, string accion, CancellationToken cancellationToken)
    {
        if (respuesta.IsSuccessStatusCode)
        {
            return;
        }

        var detalle = await respuesta.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError(
            "Qdrant | Error al {Accion} ({Codigo}): {Detalle}",
            accion, (int)respuesta.StatusCode, detalle);

        throw new InvalidOperationException(
            $"No se pudo {accion} (HTTP {(int)respuesta.StatusCode}). Revisa el log y la configuración Qdrant.");
    }
}
