using CajaApp.ViewModels;

namespace CajaApp.Views
{
    public partial class EstadisticasPage : ContentPage
    {
        private readonly EstadisticasViewModel _viewModel;

        public EstadisticasPage(EstadisticasViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.CargarAsync();
        }

        private async void OnPeriodoChanged(object sender, EventArgs e)
        {
            if (sender is Picker picker && picker.SelectedItem is string periodo)
                await _viewModel.AplicarPeriodo(periodo);
        }
    }
}
