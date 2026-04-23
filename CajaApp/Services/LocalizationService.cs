// Gestiona el idioma de la app en tiempo de ejecución.
using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace CajaApp.Services
{
    public class LocalizationService : INotifyPropertyChanged
    {
        private static LocalizationService? _instance;
        public static LocalizationService Instance =>
            _instance ??= new LocalizationService();

        private ResourceManager _resourceManager;
        private CultureInfo     _culturaActual;

        private const string PreferenceKey = "app_language";

        public static readonly string[] IdiomasSoportados = { "es", "en" };

        private LocalizationService()
        {
            _resourceManager = new ResourceManager(
                "CajaApp.Resources.Strings.AppResources",
                typeof(LocalizationService).Assembly);
            _culturaActual = DetectarCulturaInicial();
        }

        // ── Idioma actual ─────────────────────────────────────────────────────────

        public CultureInfo CulturaActual => _culturaActual;

        public string CodigoIdioma => _culturaActual.TwoLetterISOLanguageName;
        public string this[string key]
        {
            get
            {
                var valor = _resourceManager.GetString(key, _culturaActual);
                return valor ?? $"[{key}]"; 
            }
        }

        public static string Get(string key) => Instance[key];

        public static string GetF(string key, params object[] args)
        {
            var plantilla = Instance[key];
            return args.Length > 0 ? string.Format(plantilla, args) : plantilla;
        }

        // ── Cambio de idioma ──────────────────────────────────────────────────────
        public void CambiarIdioma(string codigoIso) // "es" o "en"
        {
            var nuevaCultura = new CultureInfo(codigoIso);
            if (_culturaActual.Name == nuevaCultura.Name) return;

            _culturaActual = nuevaCultura;

            // Guardar preferencia para la próxima apertura
            Preferences.Set(PreferenceKey, codigoIso);

            // Actualizar la cultura del hilo principal (afecta a DateTime.ToString, etc.)
            CultureInfo.DefaultThreadCurrentCulture   = _culturaActual;
            CultureInfo.DefaultThreadCurrentUICulture = _culturaActual;

            // Notificar a todos los bindings que todos los textos cambiaron
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }

        public bool EsEspanol => CodigoIdioma == "es";
        public bool EsIngles  => CodigoIdioma == "en";

        // ── Detección automática ──────────────────────────────────────────────────

        private static CultureInfo DetectarCulturaInicial()
        {
            // 1. Preferencia guardada por el usuario
            var guardado = Preferences.Get(PreferenceKey, string.Empty);
            if (!string.IsNullOrEmpty(guardado) && IdiomasSoportados.Contains(guardado))
                return new CultureInfo(guardado);

            // 2. Idioma del sistema
            var sistemaIso = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (IdiomasSoportados.Contains(sistemaIso))
                return new CultureInfo(sistemaIso);

            // 3. Español como fallback (mercado principal)
            return new CultureInfo("es");
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
