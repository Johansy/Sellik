namespace CajaApp.Views
{
    public partial class AcercaDePage : ContentPage
    {
        public AcercaDePage()
        {
            InitializeComponent();
        }

        private async void OnEmailTapped(object sender, EventArgs e)
        {
            try
            {
                await Launcher.OpenAsync($"mailto:qubitsoftxxi@gmail.com");
            }
            catch
            {
                // Handle case where email client is not available
            }
        }
    }
}