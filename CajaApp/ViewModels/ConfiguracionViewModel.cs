using CajaApp.Models;
using CajaApp.Services;
using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CajaApp.ViewModels
{
    public class ConfiguracionViewModel : INotifyPropertyChanged
    {
        private readonly ConfiguracionService _configuracionService;
        private readonly TemaService _temaService;

        private bool _isLoading;
        private bool _cargando;
        private bool _inicializado;
        private TemaAplicacion _temaSeleccionado;
        private string _nombreNegocio = string.Empty;
        private bool _autoGuardado;
        private bool _confirmacionEliminar;
        private bool _mostrarTutorial;
        private bool _bloquearOrientacion;
        private int _tiempoAutoGuardado;
        private string _version = string.Empty;
        private int _modoOCRIndex;
        private string _apiKeyOpenAI = string.Empty;
        private string _apiKeyGoogle = string.Empty;
        private bool _ocultarKeyOpenAI = true;
        private bool _ocultarKeyGoogle = true;

        public ObservableCollection<DenominacionConfig> Denominaciones { get; set; }
        public ObservableCollection<DenominacionConfig> DenominacionesMonedas { get; set; }
        public ObservableCollection<DenominacionConfig> DenominacionesBilletes { get; set; }

        public int ModoOCRIndex
        {
            get => _modoOCRIndex;
            set
            {
                _modoOCRIndex = value;
                OnPropertyChanged(nameof(ModoOCRIndex));
                OnPropertyChanged(nameof(DescripcionModoOCR));
            }
        }

        //Idiomas
        public int IdiomaIndex 
        {
            get => LocalizationService.Instance.EsIngles ? 1 : 0;
            set
            {
                var codigo = value == 1 ? "en" : "es";
                LocalizationService.Instance.CambiarIdioma(codigo);
                OnPropertyChanged(nameof(IdiomaIndex));
            }
        }

public string DescripcionModoOCR => _modoOCRIndex switch
        {
            0 => LocalizationService.Instance["Config_OCRDescAuto"],
            1 => LocalizationService.Instance["Config_OCRDescNativo"],
            2 => LocalizationService.Instance["Config_OCRDescGPT"],
            3 => LocalizationService.Instance["Config_OCRDescGoogle"],
            4 => LocalizationService.Instance["Config_OCRDescNativoGPT"],
            5 => LocalizationService.Instance["Config_OCRDescFusion"],
            _ => ""
        };

        public string ApiKeyOpenAI
        {
            get => _apiKeyOpenAI;
            set { _apiKeyOpenAI = value; OnPropertyChanged(nameof(ApiKeyOpenAI)); OnPropertyChanged(nameof(EstadoKeyOpenAI)); OnPropertyChanged(nameof(ColorEstadoKeyOpenAI)); }
        }

        public string ApiKeyGoogle
        {
            get => _apiKeyGoogle;
            set { _apiKeyGoogle = value; OnPropertyChanged(nameof(ApiKeyGoogle)); OnPropertyChanged(nameof(EstadoKeyGoogle)); OnPropertyChanged(nameof(ColorEstadoKeyGoogle)); }
        }

        public bool OcultarKeyOpenAI
        {
            get => _ocultarKeyOpenAI;
            set { _ocultarKeyOpenAI = value; OnPropertyChanged(nameof(OcultarKeyOpenAI)); OnPropertyChanged(nameof(IconoOcultarOpenAI)); }
        }

        public bool OcultarKeyGoogle
        {
            get => _ocultarKeyGoogle;
            set { _ocultarKeyGoogle = value; OnPropertyChanged(nameof(OcultarKeyGoogle)); OnPropertyChanged(nameof(IconoOcultarGoogle)); }
        }

        public string IconoOcultarOpenAI => _ocultarKeyOpenAI ? "👁" : "🙈";
        public string IconoOcultarGoogle => _ocultarKeyGoogle ? "👁" : "🙈";

        public string EstadoKeyOpenAI =>
            string.IsNullOrWhiteSpace(_apiKeyOpenAI) ? "⚠️ No configurada" :
            _apiKeyOpenAI.StartsWith("sk-") ? "✅ Key configurada" : "⚠️ Formato inválido (debe empezar con sk-)";

        public string EstadoKeyGoogle =>
            string.IsNullOrWhiteSpace(_apiKeyGoogle) ? "⚠️ No configurada" :
            _apiKeyGoogle.StartsWith("AIza") ? "✅ Key configurada" : "⚠️ Formato inválido (debe empezar con AIza)";

        public Color ColorEstadoKeyOpenAI =>
            _apiKeyOpenAI.StartsWith("sk-") ? Color.FromArgb("#2E7D32") : Color.FromArgb("#E65100");

        public Color ColorEstadoKeyGoogle =>
            _apiKeyGoogle.StartsWith("AIza") ? Color.FromArgb("#2E7D32") : Color.FromArgb("#E65100");

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        public TemaAplicacion TemaSeleccionado
        {
            get => _temaSeleccionado;
            set
            {
                _temaSeleccionado = value;
                OnPropertyChanged(nameof(TemaSeleccionado));
                OnPropertyChanged(nameof(TemaSeleccionadoIndex));
                if (!_cargando)
                    _ = CambiarTemaAsync(value);
            }
        }

        public int TemaSeleccionadoIndex
        {
            get => (int)_temaSeleccionado;
            set
            {
                if (value < 0) return; // ignorar el -1 que lanza el Picker al limpiar sus Items
                var tema = (TemaAplicacion)value;
                if (tema == _temaSeleccionado) return;
                _temaSeleccionado = tema;
                OnPropertyChanged(nameof(TemaSeleccionadoIndex));
                OnPropertyChanged(nameof(TemaSeleccionado));
                if (!_cargando)
                    _ = CambiarTemaAsync(tema);
            }
        }

        public string NombreNegocio
        {
            get => _nombreNegocio;
            set
            {
                _nombreNegocio = value;
                OnPropertyChanged(nameof(NombreNegocio));
                _ = GuardarConfiguracionAsync(ConfiguracionApp.Claves.NombreNegocio, value);
            }
        }

        public bool AutoGuardado
        {
            get => _autoGuardado;
            set
            {
                _autoGuardado = value;
                OnPropertyChanged(nameof(AutoGuardado));
                _ = GuardarConfiguracionAsync(ConfiguracionApp.Claves.AutoGuardado, value.ToString().ToLower());
            }
        }

        public bool ConfirmacionEliminar
        {
            get => _confirmacionEliminar;
            set
            {
                _confirmacionEliminar = value;
                OnPropertyChanged(nameof(ConfirmacionEliminar));
                _ = GuardarConfiguracionAsync(ConfiguracionApp.Claves.ConfirmacionEliminar, value.ToString().ToLower());
            }
        }

        public bool MostrarTutorial
        {
            get => _mostrarTutorial;
            set
            {
                _mostrarTutorial = value;
                OnPropertyChanged(nameof(MostrarTutorial));
                _ = GuardarConfiguracionAsync(ConfiguracionApp.Claves.MostrarTutorial, value.ToString().ToLower());
            }
        }

        public bool BloquearOrientacion
        {
            get => _bloquearOrientacion;
            set
            {
                _bloquearOrientacion = value;
                OnPropertyChanged(nameof(BloquearOrientacion));
                _ = GuardarConfiguracionAsync(ConfiguracionApp.Claves.BloquearOrientacion, value.ToString().ToLower());
                AplicarOrientacion(value);
            }
        }

        private static void AplicarOrientacion(bool bloquear)
        {
#if ANDROID
            try
            {
                if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is Android.App.Activity activity)
                {
                    activity.RequestedOrientation = bloquear
                        ? Android.Content.PM.ScreenOrientation.Portrait
                        : Android.Content.PM.ScreenOrientation.Unspecified;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error aplicando orientación: {ex.Message}");
            }
#elif IOS || MACCATALYST
            try
            {
                CajaApp.Platforms.iOS.OrientationHelper.BloquearPortrait = bloquear;
                UIKit.UIViewController.AttemptRotationToDeviceOrientation();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error aplicando orientación iOS: {ex.Message}");
            }
#endif
        }

        public int TiempoAutoGuardado
        {
            get => _tiempoAutoGuardado;
            set
            {
                _tiempoAutoGuardado = value;
                OnPropertyChanged(nameof(TiempoAutoGuardado));
                OnPropertyChanged(nameof(TiempoAutoGuardadoTexto));
                _ = GuardarConfiguracionAsync(ConfiguracionApp.Claves.TiempoAutoGuardado, value.ToString());
            }
        }

        public string Version
        {
            get => _version;
            set
            {
                _version = value;
                OnPropertyChanged(nameof(Version));
            }
        }

        public string TiempoAutoGuardadoTexto =>
            LocalizationService.GetF("Config_TiempoAutoGuardadoFmt", _tiempoAutoGuardado);

        public string Desarrollador => "Tu Empresa";

        public int TotalDenominaciones => Denominaciones?.Count ?? 0;
        public int DenominacionesActivas => Denominaciones?.Count(d => d.EstaActiva) ?? 0;
        public int DenominacionesInactivas => Denominaciones?.Count(d => !d.EstaActiva) ?? 0;
        public int DenominacionesPersonalizadas => Denominaciones?.Count(d => d.EsPersonalizada) ?? 0;

        public ConfiguracionViewModel(ConfiguracionService configuracionService, TemaService temaService)
        {
            _configuracionService = configuracionService;
            _temaService = temaService;
            Denominaciones = new ObservableCollection<DenominacionConfig>();
            DenominacionesMonedas = new ObservableCollection<DenominacionConfig>();
            DenominacionesBilletes = new ObservableCollection<DenominacionConfig>();

            LocalizationService.Instance.PropertyChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(DescripcionModoOCR));
                OnPropertyChanged(nameof(TiempoAutoGuardadoTexto));
            };
        }


        public async Task InicializarAsync()
        {
            if (_inicializado) return;
            _inicializado = true;
            await _configuracionService.InicializarConfiguracionAsync();
            await CargarConfiguraciones();
            await CargarDenominaciones();
            await CargarConfigOCRAsync();
        }

        public async Task CargarConfiguraciones()
        {
            _cargando = true;
            IsLoading = true;

            try
            {
                var temaGuardado = await _configuracionService.ObtenerTemaAsync();
                TemaSeleccionado = Enum.IsDefined(typeof(TemaAplicacion), temaGuardado)
                    ? temaGuardado
                    : TemaAplicacion.Automatico;

                NombreNegocio = await _configuracionService.ObtenerConfiguracionAsync(ConfiguracionApp.Claves.NombreNegocio, "Mi Negocio");

                var autoGuardadoValor = await _configuracionService.ObtenerConfiguracionAsync(ConfiguracionApp.Claves.AutoGuardado, "true");
                AutoGuardado = ParseBoolOrDefault(autoGuardadoValor, true);

                var confirmacionValor = await _configuracionService.ObtenerConfiguracionAsync(ConfiguracionApp.Claves.ConfirmacionEliminar, "true");
                ConfirmacionEliminar = ParseBoolOrDefault(confirmacionValor, true);

                var tutorialValor = await _configuracionService.ObtenerConfiguracionAsync(ConfiguracionApp.Claves.MostrarTutorial, "true");
                MostrarTutorial = ParseBoolOrDefault(tutorialValor, true);

                var bloquearOrientacionValor = await _configuracionService.ObtenerConfiguracionAsync(ConfiguracionApp.Claves.BloquearOrientacion, "true");
                _bloquearOrientacion = ParseBoolOrDefault(bloquearOrientacionValor, true);
                OnPropertyChanged(nameof(BloquearOrientacion));
                AplicarOrientacion(_bloquearOrientacion);

                var tiempoAutoGuardadoValor = await _configuracionService.ObtenerConfiguracionAsync(ConfiguracionApp.Claves.TiempoAutoGuardado, "30");
                TiempoAutoGuardado = ParseIntOrDefault(tiempoAutoGuardadoValor, 30, 10, 300);

                Version = await _configuracionService.ObtenerConfiguracionAsync(ConfiguracionApp.Claves.Version, "1.0.0");
            }
            finally
            {
                _cargando = false;
                IsLoading = false;
            }
        }

        private async Task CargarConfigOCRAsync()
        {
            var modo = await _configuracionService.ObtenerConfiguracionAsync(
                ConfiguracionApp.Claves.OCRModo, "0");
            _modoOCRIndex = int.TryParse(modo, out var m) ? m : 0;
            OnPropertyChanged(nameof(ModoOCRIndex));
            OnPropertyChanged(nameof(DescripcionModoOCR));

            ApiKeyOpenAI = await _configuracionService.ObtenerConfiguracionAsync(
                ConfiguracionApp.Claves.OCRApiKeyOpenAI, "");
            ApiKeyGoogle = await _configuracionService.ObtenerConfiguracionAsync(
                ConfiguracionApp.Claves.OCRApiKeyGoogle, "");
        }

        private async Task GuardarConfigOCRAsync()
        {
            try
            {
                await _configuracionService.GuardarConfiguracionAsync(
                    ConfiguracionApp.Claves.OCRModo, _modoOCRIndex.ToString(),
                    "Modo OCR: 0=Auto, 1=Nativo, 2=GPT-4o, 3=Google Vision");

                await _configuracionService.GuardarConfiguracionAsync(
                    ConfiguracionApp.Claves.OCRApiKeyOpenAI, _apiKeyOpenAI.Trim(),
                    "API key de OpenAI para GPT-4o Vision");

                await _configuracionService.GuardarConfiguracionAsync(
                    ConfiguracionApp.Claves.OCRApiKeyGoogle, _apiKeyGoogle.Trim(),
                    "API key de Google Cloud Vision");

                // Notificar al usuario
                var page = Application.Current?.Windows[0].Page;
                if (page != null)
                    await page.DisplayAlert(
                        "✅ Guardado", "Configuración OCR guardada correctamente.", "OK");
            }
            catch (Exception ex)
            {
                var page = Application.Current?.Windows[0].Page;
                if (page != null)
                    await page.DisplayAlert(
                        "Error", $"No se pudo guardar: {ex.Message}", "OK");
            }
        }

        private static bool ParseBoolOrDefault(string? value, bool defaultValue)
        {
            return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
        }

        private static int ParseIntOrDefault(string? value, int defaultValue, int minValue, int maxValue)
        {
            if (!int.TryParse(value, out var parsed))
                return defaultValue;

            return Math.Clamp(parsed, minValue, maxValue);
        }

        public async Task CargarDenominaciones()
        {
            try
            {
                var denominaciones = await _configuracionService.ObtenerTodasDenominacionesAsync();

                Denominaciones.Clear();
                DenominacionesMonedas.Clear();
                DenominacionesBilletes.Clear();

                foreach (var denom in denominaciones.OrderBy(d => d.OrdenVisualizacion))
                {
                    Denominaciones.Add(denom);

                    if (denom.Tipo == TipoDenominacion.Moneda)
                        DenominacionesMonedas.Add(denom);
                    else
                        DenominacionesBilletes.Add(denom);
                }

                ActualizarContadores();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando denominaciones: {ex.Message}");
            }
        }

        private void ActualizarContadores()
        {
            OnPropertyChanged(nameof(TotalDenominaciones));
            OnPropertyChanged(nameof(DenominacionesActivas));
            OnPropertyChanged(nameof(DenominacionesInactivas));
            OnPropertyChanged(nameof(DenominacionesPersonalizadas));
        }

        private async Task GuardarConfiguracionAsync(string clave, string valor
)
        {
            try
            {
                await _configuracionService.GuardarConfiguracionAsync(clave, valor);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guardando configuración {clave}: {ex.Message}");
            }
        }

        private async Task CambiarTemaAsync(TemaAplicacion tema)
        {
            try
            {
                await _temaService.CambiarTemaAsync(tema);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cambiando tema: {ex.Message}");
            }
        }

        public async Task<bool> CambiarEstadoDenominacionAsync(DenominacionConfig denominacion, bool nuevoEstado)
        {
            try
            {
                bool resultado = await _configuracionService.ActualizarEstadoDenominacionAsync(
                    denominacion.Id, nuevoEstado);

                if (resultado)
                {
                    denominacion.EstaActiva = nuevoEstado;
                    ActualizarContadores();
                    DenominacionesCambiadas?.Invoke(this, EventArgs.Empty);
                }

                return resultado;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> AgregarDenominacionAsync(decimal valor, string simbolo, TipoDenominacion tipo, string color)
        {
            try
            {
                bool resultado = await _configuracionService.AgregarDenominacionPersonalizadaAsync(valor, simbolo, tipo, color);
                if (resultado)
                {
                    await CargarDenominaciones();
                    DenominacionesCambiadas?.Invoke(this, EventArgs.Empty);
                }

                return resultado;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> EliminarDenominacionAsync(DenominacionConfig denominacion)
        {
            try
            {
                if (!denominacion.EsPersonalizada)
                    return false; // No se pueden eliminar denominaciones predeterminadas

                bool resultado = await _configuracionService.EliminarDenominacionPersonalizadaAsync(denominacion.Id);
                if (resultado)
                {
                    await CargarDenominaciones();
                    DenominacionesCambiadas?.Invoke(this, EventArgs.Empty);
                }

                return resultado;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RestaurarConfiguracionAsync()
        {
            try
            {
                bool resultado = await _configuracionService.RestaurarConfiguracionPredeterminadaAsync();
                if (resultado)
                {
                    await CargarConfiguraciones();
                    await CargarDenominaciones();
                }
                return resultado;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error restaurando configuración: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Genera un resumen legible de las configuraciones actuales.
        /// </summary>
        public string GenerarResumenConfiguracion()
        {
            // Construye un resumen simple; ajusta campos según necesites
            return $"Negocio: {NombreNegocio}\n" +
                   $"Tema: {TemaSeleccionado}\n" +
                   $"AutoGuardado: {AutoGuardado} (cada {TiempoAutoGuardado} seg)\n" +
                   $"Confirmación eliminar: {ConfirmacionEliminar}\n" +
                   $"Mostrar tutorial: {MostrarTutorial}\n" +
                   $"Versión: {Version}\n" +
                   $"Denominaciones: {TotalDenominaciones} (Activas: {DenominacionesActivas}, Personalizadas: {DenominacionesPersonalizadas})";
        }

        public async Task<bool> ExportarConfiguracionAsync()
        {
            try
            {
                // Primero, serializamos la configuración actual
                var exportObj = new
                {
                    NombreNegocio,
                    TemaSeleccionado,
                    AutoGuardado,
                    ConfirmacionEliminar,
                    MostrarTutorial,
                    TiempoAutoGuardado,
                    Version,
                    Denominaciones = Denominaciones?.Select(d => new
                    {
                        d.Id,
                        d.Valor,
                        d.Simbolo,
                        d.Tipo,
                        d.OrdenVisualizacion,
                        d.EstaActiva,
                        d.EsPersonalizada,
                        d.Color
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(exportObj, new JsonSerializerOptions { WriteIndented = true });
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

                // Usamos el selector de guardado nativo del sistema
#pragma warning disable CA1416
                var fileSaverResult = await FileSaver.Default.SaveAsync("configuracion_export.json", stream,
                    new CancellationToken());
#pragma warning restore CA1416

                if (fileSaverResult.IsSuccessful)
                {
                    System.Diagnostics.Debug.WriteLine($"Configuración exportada a: {fileSaverResult.FilePath}");
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Exportación cancelada por el usuario.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error exportando configuración: {ex.Message}");
                return false;
            }
        }


        // Implementación de INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Se dispara cuando se agrega, elimina o cambia el estado de una denominación.</summary>
        public event EventHandler? DenominacionesCambiadas;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ICommand ToggleOcultarOpenAICommand =>
            new Command(() => OcultarKeyOpenAI = !OcultarKeyOpenAI);

        public ICommand ToggleOcultarGoogleCommand =>
            new Command(() => OcultarKeyGoogle = !OcultarKeyGoogle);

        public ICommand GuardarConfigOCRCommand =>
            new Command(async () => await GuardarConfigOCRAsync());

    }
}
