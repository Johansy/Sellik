using Microsoft.Maui.Controls;
using CajaApp.ViewModels;

namespace CajaApp.Views
{
    public partial class HistorialPage : ContentPage
    {
        public HistorialPage(HistorialViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is HistorialViewModel vm)
            {
                await vm.CargarDatos();
                await vm.GenerarEstadisticas();
            }
        }
    }
}
