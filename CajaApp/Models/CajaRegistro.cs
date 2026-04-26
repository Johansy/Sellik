using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;

namespace CajaApp.Models
{
    [Table("CajaRegistro")]
    public class CajaRegistro
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string NombreNota { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public decimal Total { get; set; }
        public string TotalTexto { get; set; } = string.Empty;

        // Cantidades de cada denominación — legado, migradas a DenominacionValores
        [Ignore] public int Centavos1 { get; set; }
        [Ignore] public int Centavos5 { get; set; }
        [Ignore] public int Centavos10 { get; set; }
        [Ignore] public int Centavos20 { get; set; }
        [Ignore] public int Centavos50 { get; set; }
        [Ignore] public int Peso1 { get; set; }
        [Ignore] public int Peso2 { get; set; }
        [Ignore] public int Peso5 { get; set; }
        [Ignore] public int Peso10 { get; set; }
        [Ignore] public int Peso20 { get; set; }
        [Ignore] public int Billete20 { get; set; }
        [Ignore] public int Billete50 { get; set; }
        [Ignore] public int Billete100 { get; set; }
        [Ignore] public int Billete200 { get; set; }
        [Ignore] public int Billete500 { get; set; }
        [Ignore] public int Billete1000 { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        // FK a Sesion (0 = datos sin sesión / legado)
        public int SesionId { get; set; } = 0;
    }
}
