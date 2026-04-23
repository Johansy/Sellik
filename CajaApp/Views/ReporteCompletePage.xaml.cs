namespace CajaApp.Views
{
    public partial class ReporteCompletePage : ContentPage
    {
        private readonly string _contenidoReporte;

        public ReporteCompletePage(string contenidoReporte)
        {
            InitializeComponent();
            _contenidoReporte = contenidoReporte;
            ReporteLabel.Text = contenidoReporte;
        }

        private async void OnCompartirReporteClicked(object sender, EventArgs e)
        {
            try
            {
                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Text = _contenidoReporte,
                    Title = "Reporte Completo de Historial"
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo compartir: {ex.Message}", "OK");
            }
        }

        private async void OnCerrarClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}
