using _20262.ML;
using _20262.Models.Entities;

namespace _20262.Models.ViewModels;

/// <summary>Página de recomendaciones para el usuario logueado.</summary>
public class RecomendacionesViewModel
{
    /// <summary>True si el modelo está cargado/entrenado y listo para predecir.</summary>
    public bool ModeloListo { get; set; }

    /// <summary>True cuando el usuario (Guid de Identity) aparece en el dataset de ratings.</summary>
    public bool UsuarioConocido { get; set; }

    /// <summary>Cantidad de productos solicitados (TopK).</summary>
    public int TopK { get; set; }

    public List<RecomendacionProductoVM> Recomendaciones { get; set; } = [];

    public MetricasEntrenamientoRecomendacion? Metricas { get; set; }
}

/// <summary>Producto recomendado junto a la calificación predicha por el modelo.</summary>
public class RecomendacionProductoVM
{
    public Producto Producto { get; set; } = null!;

    /// <summary>Calificación predicha (1-5) por la factorización de matrices o promedio de popularidad.</summary>
    public float Score { get; set; }

    /// <summary>True cuando la recomendación vino de la popularidad (usuario sin historial en el dataset).</summary>
    public bool DeFrio { get; set; }
}