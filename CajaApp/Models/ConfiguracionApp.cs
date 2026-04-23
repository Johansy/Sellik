using SQLite;

namespace CajaApp.Models
{
    [Table("ConfiguracionApp")]
    public class ConfiguracionApp
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Clave { get; set; } = string.Empty;
        public string Valor { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public DateTime FechaModificacion { get; set; } = DateTime.Now;

        public static class Claves
        {
            // ── General ──────────────────────────────────────────────────────────
            public const string TemaApp             = "TemaApp";
            public const string DenominacionesActivas = "DenominacionesActivas";
            public const string NombreNegocio       = "NombreNegocio";
            public const string MonedaPrincipal     = "MonedaPrincipal";
            public const string FormatoFecha        = "FormatoFecha";
            public const string AutoGuardado        = "AutoGuardado";
            public const string ConfirmacionEliminar = "ConfirmacionEliminar";
            public const string TiempoAutoGuardado  = "TiempoAutoGuardado";
            public const string MostrarTutorial     = "MostrarTutorial";
            public const string Version             = "Version";

            // ── Pantalla ──────────────────────────────────────────────────────────
            public const string BloquearOrientacion = "BloquearOrientacion";

            // ── OCR en la nube ────────────────────────────────────────────────────
            public const string OCRModo          = "OCRModo";
            public const string OCRApiKeyOpenAI  = "OCRApiKeyOpenAI";
            public const string OCRApiKeyGoogle  = "OCRApiKeyGoogle";
        }
    }

    public enum TemaAplicacion
    {
        Claro     = 0,
        Oscuro    = 1,
        Automatico = 2
    }
}
