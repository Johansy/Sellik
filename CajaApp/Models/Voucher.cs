using CajaApp.Models;
using CajaApp.Services;
using SQLite;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace CajaApp.Models
{
    [Table("Vouchers")]
    public class Voucher
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public DateTime Fecha { get; set; }
        public string NumeroVoucher { get; set; } = string.Empty;
        public string Comercio { get; set; } = string.Empty;
        public TipoPago TipoPago { get; set; } // Crédito, Débito, etc.
        public string UltimosDigitosTarjeta { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal Impuestos { get; set; }
        public decimal Descuentos { get; set; }
        public decimal Total { get; set; }
        public string Moneda { get; set; } = "MXN";
        public string NumeroAutorizacion { get; set; } = string.Empty;
        public string ReferenciaBanco { get; set; } = string.Empty;
        public string TextoCompleto { get; set; } = string.Empty;
        public string RutaImagen { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public string Notas { get; set; } = string.Empty;
        public string TextoManuscrito { get; set; } = string.Empty;

        // FK a Sesion (0 = datos sin sesión / legado)
        public int SesionId { get; set; } = 0;

        // Propiedades calculadas
        [Ignore]
        public string TipoPagoTexto => TipoPago.ToString().ToUpper();

        [Ignore]
        public string TotalTexto => $"${Total:F2}";

        [Ignore]
        public string FechaTexto => Fecha.ToString("dd/MM/yyyy HH:mm");

        [Ignore]
        public string ColorTipo => TipoPago switch
        {
            TipoPago.Credito => "#FF5722",
            TipoPago.Debito => "#2196F3",
            TipoPago.Efectivo => "#4CAF50",
            _ => "#757575"
        };
    }

    public enum TipoPago
    {
        Credito = 1,
        Debito = 2,
        Efectivo = 3,
        Transferencia = 4,
        Otro = 5
    }
}