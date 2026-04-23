using SQLite;

namespace CajaApp.Models
{
    [Table("Sesiones")]
    public class Sesion
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public DateTime FechaUltimoAcceso { get; set; } = DateTime.Now;

        [Ignore]
        public string FechaCreacionTexto => FechaCreacion.ToString("dd/MM/yyyy");

        [Ignore]
        public string FechaUltimoAccesoTexto => FechaUltimoAcceso.ToString("dd/MM/yyyy HH:mm");
    }
}
