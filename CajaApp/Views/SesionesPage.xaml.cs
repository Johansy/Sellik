using CajaApp.Models;
using CajaApp.Services;
using CajaApp.ViewModels;

namespace CajaApp.Views
{
    public partial class SesionesPage : ContentPage
    {
        private readonly SesionesViewModel _vm;
        private bool _eliminando;

        public SesionesPage(SesionesViewModel viewModel)
        {
            InitializeComponent();
            _vm = viewModel;
            _vm.SesionSeleccionada += OnSesionSeleccionada;
            BindingContext = _vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.CargarSesionesAsync();
        }

        private void OnSesionSeleccionada(Sesion sesion)
        {
            // Navegar al shell principal resolviendo desde el contenedor de DI
            var shell = IPlatformApplication.Current!.Services.GetRequiredService<AppShell>();
            Application.Current!.Windows[0].Page = shell;
        }

        private void OnSesionTapped(object sender, TappedEventArgs e)
        {
            if (_eliminando) return;

            if (sender is Border border && border.BindingContext is Sesion sesion)
                _vm.AbrirSesionCommand.Execute(sesion);
        }

        internal async Task EliminarSesionAsync(Sesion sesion)
        {
            _eliminando = true;

            try
            {
                bool confirmar = await DisplayAlert(
                    LocalizationService.Instance["Sesiones_EliminarTitulo"],
                    LocalizationService.Instance["Sesiones_ConfirmarEliminar"],
                    LocalizationService.Instance["Btn_Eliminar"],
                    LocalizationService.Instance["Btn_Cancelar"]);

                if (!confirmar)
                    return;

                await _vm.EliminarSesionAsync(sesion);
            }
            finally
            {
                _eliminando = false;
            }
        }

        private async void OnEliminarClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.BindingContext is Sesion sesion)
                await EliminarSesionAsync(sesion);
        }

        private async void OnSwipeEliminarInvoked(object sender, EventArgs e)
        {
            if (sender is SwipeItem swipe && swipe.BindingContext is Sesion sesion)
                await EliminarSesionAsync(sesion);
        }
    }
}
