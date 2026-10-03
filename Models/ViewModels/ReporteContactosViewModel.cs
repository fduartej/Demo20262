using System.ComponentModel.DataAnnotations;

namespace _20262.Models.ViewModels;

/// <summary>Fila del reporte de mensajes de contacto con el sentimiento detectado por el modelo.</summary>
public class ContactoReporteViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTime FechaRegistro { get; set; }

    /// <summary>Etiqueta almacenada al recibir el mensaje: "Positivo" o "Negativo" (null si no se clasificó).</summary>
    public string? Sentimiento { get; set; }

    /// <summary>Probabilidad (0-1) de la clase predicha.</summary>
    public double? Probabilidad { get; set; }

    public bool EsPositivo => Sentimiento == "Positivo";

    /// <summary>Confianza del modelo expresada en porcentaje (0-100).</summary>
    public double Confianza => Probabilidad is null ? 0 : Math.Round(Probabilidad.Value * 100, 1);
}

/// <summary>Reporte diario de mensajes de contacto con el resumen de sentimiento del modelo.</summary>
public class ReporteContactosViewModel
{
    public DateTime Fecha { get; set; } = DateTime.Today;

    /// <summary>Filtro activo sobre la tabla: todos, positivos o negativos.</summary>
    public string Filtro { get; set; } = "todos";

    public List<ContactoReporteViewModel> Mensajes { get; set; } = [];

    /// <summary>Mensajes del día después de aplicar <see cref="Filtro"/>.</summary>
    public IEnumerable<ContactoReporteViewModel> MensajesFiltrados => Filtro switch
    {
        "positivos" => Mensajes.Where(m => m.EsPositivo),
        "negativos" => Mensajes.Where(m => m.Sentimiento == "Negativo"),
        _ => Mensajes
    };

    public int TotalMensajes => Mensajes.Count;

    public int Positivos => Mensajes.Count(m => m.EsPositivo);

    public int Negativos => Mensajes.Count(m => m.Sentimiento == "Negativo");

    public int SinClasificar => Mensajes.Count(m => m.Sentimiento is null);

    public double ConfianzaPromedio
    {
        get
        {
            var clasificados = Mensajes.Where(m => m.Probabilidad is not null).ToList();
            return clasificados.Count == 0
                ? 0
                : Math.Round(clasificados.Average(m => m.Confianza), 1);
        }
    }

    /// <summary>Porcentaje de mensajes negativos (0-100), la métrica que el admin debe vigilar.</summary>
    public double PorcentajeNegativo => TotalMensajes == 0
        ? 0
        : Math.Round(Negativos * 100d / TotalMensajes, 1);

    [DataType(DataType.Date)]
    [Display(Name = "Fecha")]
    public DateTime FechaSeleccionada
    {
        get => Fecha;
        set => Fecha = value.Date;
    }
}
