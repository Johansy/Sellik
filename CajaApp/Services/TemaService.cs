using CajaApp.Models;
#if ANDROID
using Android.Content.Res;
using AndroidX.AppCompat.App;
using Microsoft.Maui.ApplicationModel;
#endif

namespace CajaApp.Services
{
    public class TemaService
    {
        public static TemaService Instance { get; } = new TemaService();

        private TemaAplicacion _temaActual = TemaAplicacion.Claro;
        private bool _isRequestedThemeSubscribed;

        private TemaService()
        {
        }

        private ConfiguracionService ObtenerConfiguracionService()
        {
            return IPlatformApplication.Current!.Services.GetRequiredService<ConfiguracionService>();
        }

        public async Task AplicarTemaAsync()
        {
            var tema = await ObtenerConfiguracionService().ObtenerTemaAsync();
            AplicarTema(tema);
        }

        public async Task<bool> CambiarTemaAsync(TemaAplicacion nuevoTema)
        {
            bool resultado = await ObtenerConfiguracionService().CambiarTemaAsync(nuevoTema);
            AplicarTema(nuevoTema);
            return resultado;
        }

        private void AplicarTema(TemaAplicacion tema)
        {
            if (Application.Current == null) return;

            // Desuscribir si salimos del modo Automático
            if (_temaActual == TemaAplicacion.Automatico && tema != TemaAplicacion.Automatico && _isRequestedThemeSubscribed)
            {
                Application.Current.RequestedThemeChanged -= OnRequestedThemeChanged;
                _isRequestedThemeSubscribed = false;
            }

            _temaActual = tema;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (Application.Current == null) return;

#if ANDROID
                SincronizarModoAndroid(tema);
#endif

                Application.Current.UserAppTheme = tema switch
                {
                    TemaAplicacion.Oscuro => AppTheme.Dark,
                    TemaAplicacion.Automatico => AppTheme.Unspecified,
                    _ => AppTheme.Light
                };

                bool oscuro = ObtenerTemaEfectivo(Application.Current) == AppTheme.Dark;
                AplicarColoresEnRecursos(oscuro);

                // Suscribir al cambio de tema del OS solo en modo Automático
                if (tema == TemaAplicacion.Automatico && !_isRequestedThemeSubscribed)
                {
                    Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
                    _isRequestedThemeSubscribed = true;
                }
            });
        }

        private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
        {
            if (_temaActual != TemaAplicacion.Automatico) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                bool oscuro = e.RequestedTheme == AppTheme.Dark;
                AplicarColoresEnRecursos(oscuro);
            });
        }

        private static void AplicarColoresEnRecursos(bool oscuro)
        {
            if (Application.Current?.Resources == null) return;

            var res = Application.Current.Resources;
            if (oscuro)
            {
                res["BackgroundColor"] = Color.FromArgb("#121212");
                res["TextColor"] = Colors.White;
                res["SurfaceColor"] = Color.FromArgb("#1E1E1E");
                res["CardColor"] = Color.FromArgb("#2D2D2D");
                res["BorderColor"] = Color.FromArgb("#333333");
                res["PrimaryColor"] = Color.FromArgb("#4CAF50");
                res["SecondaryColor"] = Color.FromArgb("#FFB74D");
            }
            else
            {
                res["BackgroundColor"] = Colors.White;
                res["TextColor"] = Colors.Black;
                res["SurfaceColor"] = Color.FromArgb("#F5F5F5");
                res["CardColor"] = Colors.White;
                res["BorderColor"] = Color.FromArgb("#E0E0E0");
                res["PrimaryColor"] = Color.FromArgb("#2E7D32");
                res["SecondaryColor"] = Color.FromArgb("#FF9800");
            }
        }

        private static AppTheme ObtenerTemaEfectivo(Application app)
        {
            if (app.UserAppTheme != AppTheme.Unspecified)
                return app.UserAppTheme;

            if (app.RequestedTheme != AppTheme.Unspecified)
                return app.RequestedTheme;

            return EsSistemaOscuro() ? AppTheme.Dark : AppTheme.Light;
        }


        public static bool EsSistemaOscuro()
        {
#if ANDROID
            try
            {
                var config = Platform.AppContext.Resources?.Configuration;
                return (config?.UiMode & UiMode.NightMask) == UiMode.NightYes;
            }
            catch { return false; }
#else
            return Application.Current?.RequestedTheme == AppTheme.Dark;
#endif
        }

#if ANDROID
        private static void SincronizarModoAndroid(TemaAplicacion tema)
        {
            int nightMode = tema switch
            {
                TemaAplicacion.Oscuro => AppCompatDelegate.ModeNightYes,
                TemaAplicacion.Automatico => AppCompatDelegate.ModeNightFollowSystem,
                _ => AppCompatDelegate.ModeNightNo
            };

            AppCompatDelegate.DefaultNightMode = nightMode;
        }
#endif
    }
}

