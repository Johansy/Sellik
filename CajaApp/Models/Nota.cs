using SQLite;

namespace CajaApp.Models
{
    [Table("Notas")]
    public class Nota
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Titulo { get; set; } = string.Empty;
        public string Contenido { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public TipoNota Tipo { get; set; }
        public string RutaImagen { get; set; } = string.Empty;
        public string NombreArchivoImagen { get; set; } = string.Empty;
        public bool TieneImagen => !string.IsNullOrEmpty(RutaImagen);
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public DateTime FechaModificacion { get; set; } = DateTime.Now;
        public bool EsFavorita { get; set; }

        // FK a Sesion (0 = datos sin sesión / legado)
        public int SesionId { get; set; } = 0;
        public string Etiquetas { get; set; } = string.Empty;

        // Propiedades calculadas
        [Ignore]
        public string TipoTexto => Tipo switch
        {
            TipoNota.Texto => "📝 Texto",
            TipoNota.Imagen => "📷 Imagen",
            TipoNota.TextoConImagen => "📝📷 Mixta",
            _ => "📄 General"
        };

        [Ignore]
        public string FechaTexto => Fecha.ToString("dd/MM/yyyy HH:mm");

        [Ignore]
        public string FechaCorta => Fecha.ToString("dd/MM/yyyy");

        [Ignore]
        public string ResumenContenido => string.IsNullOrEmpty(Contenido)
            ? "Sin contenido"
            : Contenido.Length > 100
                ? Contenido.Substring(0, 97) + "..."
                : Contenido;

        [Ignore]
        public string IconoFavorita => EsFavorita ? "⭐" : "☆";

        [Ignore]
        public string ColorTipo => Tipo switch
        {
            TipoNota.Texto => "#2196F3",
            TipoNota.Imagen => "#4CAF50",
            TipoNota.TextoConImagen => "#FF9800",
            _ => "#757575"
        };

        [Ignore]
        public List<string> ListaEtiquetas => string.IsNullOrEmpty(Etiquetas)
            ? new List<string>()
            : Etiquetas.Split(',').Select(e => e.Trim()).Where(e => !string.IsNullOrEmpty(e)).ToList();
    }

    public enum TipoNota
    {
        Texto = 1,
        Imagen = 2,
        TextoConImagen = 3
    }
}
