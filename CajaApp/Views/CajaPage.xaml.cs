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
#pragma warning disable CS0618
            MessagingCenter.Subscribe<object>(this, "DenominacionesCambiadas", async _ =>
            {
                await MainThread.InvokeOnMainThreadAsync(() => _viewModel.RefrescarDenominacionesAsync());
            });
#pragma warning restore CS0618
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.RefrescarDenominacionesAsync();
        }
    }
}
