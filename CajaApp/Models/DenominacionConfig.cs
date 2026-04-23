using SQLite;

namespace CajaApp.Models
{
    [Table("DenominacionesConfig")]
    public class DenominacionConfig
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public decimal Valor { get; set; }
        public string Simbolo { get; set; } = string.Empty;
        public TipoDenominacion Tipo { get; set; }
        public bool EstaActiva { get; set; } = true;
        public int OrdenVisualizacion { get; set; }
        public string Color { get; set; } = string.Empty;
        public bool EsPersonalizada { get; set; } = false;
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public DateTime FechaModificacion { get; set; } = DateTime.Now;

        // Propiedades calculadas
        [Ignore]
        public string ValorTexto => Tipo == TipoDenominacion.Moneda
            ? $"${Valor:F2}"
            : $"${Valor:F0}";

        [Ignore]
        public string DescripcionCompleta => $"{Simbolo} - {ValorTexto} ({(Tipo == TipoDenominacion.Moneda ? "Moneda" : "Billete")})";

        [Ignore]
        public string EstadoTexto => EstaActiva ? "✓ Activa" : "✗ Inactiva";

        [Ignore]
        public string ColorEstado => EstaActiva ? "#4CAF50" : "#F44336";

        [Ignore]
        public Microsoft.Maui.Graphics.Color ColorEstadoColor => EstaActiva
            ? Microsoft.Maui.Graphics.Color.FromArgb("#4CAF50")
            : Microsoft.Maui.Graphics.Color.FromArgb("#F44336");

        [Ignore]
        public Microsoft.Maui.Graphics.Color ColorValue
        {
            get
            {
                try { return Microsoft.Maui.Graphics.Color.FromArgb(this.Color); }
                catch { return Microsoft.Maui.Graphics.Color.FromArgb("#9E9E9E"); }
            }
        }
    }
}
