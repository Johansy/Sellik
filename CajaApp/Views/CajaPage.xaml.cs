using Microsoft.Maui.Controls;
using CajaApp.ViewModels;
using System;

namespace CajaApp.Views
{
    public partial class CajaPage : ContentPage
    {
        private readonly CajaViewModel _viewModel;

        public CajaPage(CajaViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            BindingContext = _viewModel;

            _viewModel.OnGuardadoResultado = async (exito) =>
            {
                if (exito)
                    await DisplayAlert("Guardado", "El conteo de caja fue guardado exitosamente.", "Aceptar");
                else
                    await DisplayAlert("Error", "No se pudo guardar el conteo de caja.", "Aceptar");
            };

            // Refrescar denominaciones cuando cambie la configuración desde otro modal
            if (IPlatformApplication.Current?.Services.GetService<ConfiguracionViewModel>() is { } configVm)
                configVm.DenominacionesCambiadas += async (_, _) =>
                    await MainThread.InvokeOnMainThreadAsync(() => _viewModel.RefrescarDenominacionesAsync());

            // Cargar registro cuando el Historial solicite editar una caja
            if (IPlatformApplication.Current?.Services.GetService<HistorialViewModel>() is { } historialVm)
                historialVm.EditarCajaSolicitado += async (_, registroId) =>
                    await MainThread.InvokeOnMainThreadAsync(() => _viewModel.LoadRegistroAsync(registroId));
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.RefrescarDenominacionesAsync();
        }
    }
}
