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

        // Cantidades de cada denominación
        public int Centavos1 { get; set; }
        public int Centavos5 { get; set; }
        public int Centavos10 { get; set; }
        public int Centavos20 { get; set; }
        public int Centavos50 { get; set; }
        public int Peso1 { get; set; }
        public int Peso2 { get; set; }
        public int Peso5 { get; set; }
        public int Peso10 { get; set; }
        public int Peso20 { get; set; }
        public int Billete20 { get; set; }
        public int Billete50 { get; set; }
        public int Billete100 { get; set; }
        public int Billete200 { get; set; }
        public int Billete500 { get; set; }
        public int Billete1000 { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        // FK a Sesion (0 = datos sin sesión / legado)
        public int SesionId { get; set; } = 0;
    }
}
