using CajaApp.Services;
using CajaApp.Views;
using CajaApp.ViewModels;
using System.Windows.Input;

namespace CajaApp
{
    public partial class AppShell : Shell
    {
        public ICommand CerrarSesionCommand    { get; }
        public ICommand IrAHistorialCommand    { get; }
        public ICommand IrAEstadisticasCommand { get; }
        public ICommand IrAConfiguracionCommand{ get; }

        // Instancias cacheadas para evitar reinicializaciones y datos duplicados
        private HistorialPage?    _historialPage;
        private EstadisticasPage? _estadisticasPage;
        private ConfiguracionPage? _configuracionPage;

        public AppShell()
        {
            this.InitializeComponent();

            CerrarSesionCommand     = new Command(CerrarSesion);
            IrAHistorialCommand     = new Command(async () => await AbrirModal(ObtenerHistorial()));
            IrAEstadisticasCommand  = new Command(async () => await AbrirModal(ObtenerEstadisticas()));
            IrAConfiguracionCommand = new Command(async () => await AbrirModal(ObtenerConfiguracion()));

            BindingContext = this;

            ActualizarNombreSesion();
            SesionService.Instance.SesionCambiada += (_, _) =>
            {
                // Al cambiar de sesión, invalidar caché para que la nueva sesión cargue datos frescos
                _historialPage    = null;
                _estadisticasPage = null;
                _configuracionPage = null;
                ActualizarNombreSesion();
            };
        }

        private HistorialPage ObtenerHistorial()
        {
            if (_historialPage == null)
            {
                var vm = IPlatformApplication.Current!.Services.GetRequiredService<HistorialViewModel>();
                _historialPage = new HistorialPage(vm);
            }
            return _historialPage;
        }

        private EstadisticasPage ObtenerEstadisticas()
        {
            if (_estadisticasPage == null)
            {
                var vm = IPlatformApplication.Current!.Services.GetRequiredService<EstadisticasViewModel>();
                _estadisticasPage = new EstadisticasPage(vm);
            }
            return _estadisticasPage;
        }

        private ConfiguracionPage ObtenerConfiguracion()
        {
            if (_configuracionPage == null)
            {
                var vm = IPlatformApplication.Current!.Services.GetRequiredService<ConfiguracionViewModel>();
                _configuracionPage = new ConfiguracionPage(vm);
            }
            return _configuracionPage;
        }

        private async Task AbrirModal(Page pagina)
        {
            FlyoutIsPresented = false;
            await Navigation.PushModalAsync(new NavigationPage(pagina));
        }

        private void ActualizarNombreSesion()
        {
            LblNombreSesion.Text = SesionService.Instance.SesionActualNombre;
        }

        private void CerrarSesion()
        {
            SesionService.Instance.CerrarSesion();
            var sesionesPage = IPlatformApplication.Current!.Services.GetRequiredService<SesionesPage>();
            Application.Current!.Windows[0].Page = new NavigationPage(sesionesPage);
        }
    }
}
