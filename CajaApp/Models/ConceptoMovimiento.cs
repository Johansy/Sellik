using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CajaApp.Services;
using SQLite;

namespace CajaApp.Models
{

        [Table("ConceptoMovimiento")]
        public class ConceptoMovimiento
        {
            [PrimaryKey, AutoIncrement]
            public int Id { get; set; }
            public string Nombre { get; set; } = string.Empty;

            /// Clave de localización. Si está definida, el nombre se resuelve
            /// dinámicamente según el idioma activo.
            public string Clave { get; set; } = string.Empty;

            public TipoMovimiento Tipo { get; set; }
            public bool EsActivo { get; set; } = true;
            public DateTime FechaCreacion { get; set; } = DateTime.Now;

            /// Nombre localizado: usa la clave de recursos si existe, si no cae
            /// al texto almacenado en <see cref="Nombre"/>.
            [Ignore]
            public string NombreLocalizado =>
                !string.IsNullOrEmpty(Clave)
                    ? LocalizationService.Instance[Clave]
                    : Nombre;
        }
}
