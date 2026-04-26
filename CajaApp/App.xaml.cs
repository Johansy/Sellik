using CajaApp.Models;
using CajaApp.Services;
using CajaApp.Views;
using System.Diagnostics;

namespace CajaApp
{
    public partial class App : Application
    {
        public App(DatabaseService db, LicenseService licenseService)
        {
            _ = db; // Inyectado para asegurar que la DB se inicializa al arrancar
            // Inicializar servicio de licencias (valida clave guardada o activa DEBUG mode)
            FireAndForget(licenseService.InicializarAsync(), "LicenseService.InicializarAsync");
            // Aplicar UserAppTheme antes de InitializeComponent para que
            // los recursos DynamicResource arranquen con los colores correctos.
            AplicarTemaInicial();
            InitializeComponent();
            // Aplicar colores en el diccionario de recursos tras InitializeComponent.
            AplicarColoresIniciales();

            // Sincroniza estado interno del servicio de tema y suscripción al tema del sistema.
            FireAndForget(TemaService.Instance.AplicarTemaAsync(), "TemaService.AplicarTemaAsync");
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var sesionesPage = IPlatformApplication.Current!.Services.GetRequiredService<SesionesPage>();
            return new Window(new NavigationPage(sesionesPage));
        }

        private void AplicarTemaInicial()
        {
            try
            {
                var temaStr = Preferences.Get(ConfiguracionApp.Claves.TemaApp, "2");
                int.TryParse(temaStr, out int temaInt);

                UserAppTheme = (TemaAplicacion)temaInt switch
                {
                    TemaAplicacion.Oscuro => AppTheme.Dark,
                    TemaAplicacion.Automatico => AppTheme.Unspecified,
                    _ => AppTheme.Light
                };
            }
            catch
            {
                UserAppTheme = AppTheme.Unspecified;
            }
        }

        private void AplicarColoresIniciales()
        {
            try
            {
                var temaStr = Preferences.Get(ConfiguracionApp.Claves.TemaApp, "2");
                int.TryParse(temaStr, out int temaInt);
                bool oscuro = (TemaAplicacion)temaInt switch
                {
                    TemaAplicacion.Oscuro     => true,
                    TemaAplicacion.Automatico => TemaService.EsSistemaOscuro(),
                    _                         => false
                };

                var res = Resources;

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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error aplicando colores iniciales: {ex.Message}");
            }
        }

        private static async void FireAndForget(Task task, string operationName)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] Error en {operationName}: {ex}");
            }
        }
    }
}