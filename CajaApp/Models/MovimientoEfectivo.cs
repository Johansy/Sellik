using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SQLite;
using CajaApp.Services;

namespace CajaApp.Models
{
    [Table("MovimientoEfectivo")]
    public class MovimientoEfectivo
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public TipoMovimiento Tipo { get; set; } // Entrada o Salida
        public decimal Monto { get; set; }
        public string Concepto { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Responsable { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        // FK a Sesion (0 = datos sin sesión / legado)
        public int SesionId { get; set; } = 0;

        //Propiedades Calculadas
        [Ignore]
        public string TipoTexto => Tipo == TipoMovimiento.Entrada
            ? LocalizationService.Instance["Mov_TipoEntrada"]
            : LocalizationService.Instance["Mov_TipoSalida"];

        [Ignore]
        public string MontoTexto => Tipo == TipoMovimiento.Entrada
            ? $"+${Monto:F2}"
            : $"-${Monto:F2}";

        [Ignore]
        public Color ColorTipoTexto => Tipo == TipoMovimiento.Entrada
            ? Color.FromArgb("#4CAF50")
            : Color.FromArgb("#F44336");

        [Ignore]
        public Brush ColorTipoFondo => new SolidColorBrush(ColorTipoTexto);
    }

    public enum TipoMovimiento
    {
        Entrada = 1,
        Salida = 2
    }
}
