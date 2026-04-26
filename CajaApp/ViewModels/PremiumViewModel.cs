// ViewModels/PremiumViewModel.cs
using CajaApp.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CajaApp.ViewModels
{
    public class PremiumViewModel : INotifyPropertyChanged
    {
        private readonly LicenseService _license;

        private string _claveIngresada = string.Empty;
        private bool   _isBusy;
        private string _mensajeEstado = string.Empty;
        private bool   _mensajeExito;

        public PremiumViewModel(LicenseService license)
        {
            _license = license;
            ComprarUnicoCommand      = new Command(async () => await AbrirCheckoutAsync(PayPalConfig.CheckoutUrlUnico));
            ComprarMensualCommand    = new Command(async () => await AbrirCheckoutAsync(PayPalConfig.CheckoutUrlMensual));
            ActivarClaveCommand      = new Command(async () => await ActivarClaveAsync(),      () => !IsBusy);
            RestaurarCommand         = new Command(async () => await RestaurarAsync(),         () => !IsBusy);
            EnviarComprobanteCommand = new Command(async () => await EnviarComprobanteAsync());

            _license.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(LicenseService.EsPremium) or nameof(LicenseService.Plan))
                {
                    OnPropertyChanged(nameof(EsPremium));
                    OnPropertyChanged(nameof(EsFree));
                    OnPropertyChanged(nameof(TextoPlan));
                }
            };
        }

        // ── Propiedades ────────────────────────────────────────────────────────

        public bool EsPremium => _license.EsPremium;
        public bool EsFree    => _license.EsFree;
        public string TextoPlan => _license.EsPremium
            ? LocalizationService.Get("Premium_TextoPlanPremium")
            : LocalizationService.Get("Premium_TextoPlanFree");

        public string ClaveIngresada
        {
            get => _claveIngresada;
            set { _claveIngresada = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
                ((Command)ActivarClaveCommand).ChangeCanExecute();
                ((Command)RestaurarCommand).ChangeCanExecute();
            }
        }

        public string MensajeEstado
        {
            get => _mensajeEstado;
            set { _mensajeEstado = value; OnPropertyChanged(); OnPropertyChanged(nameof(HayMensaje)); }
        }

        public bool MensajeExito
        {
            get => _mensajeExito;
            set { _mensajeExito = value; OnPropertyChanged(); }
        }

        public bool HayMensaje => !string.IsNullOrEmpty(MensajeEstado);

        // ── Comandos ───────────────────────────────────────────────────────────

        public ICommand ComprarUnicoCommand      { get; }
        public ICommand ComprarMensualCommand    { get; }
        public ICommand ActivarClaveCommand      { get; }
        public ICommand RestaurarCommand         { get; }
        public ICommand EnviarComprobanteCommand { get; }

        // ── Implementación ─────────────────────────────────────────────────────

        private async Task AbrirCheckoutAsync(string url)
        {
            try
            {
                await Browser.OpenAsync(url, BrowserLaunchMode.SystemPreferred);
            }
            catch (Exception ex)
            {
                MensajeExito = false;
                MensajeEstado = LocalizationService.GetF("Premium_ErrorNavegador", ex.Message);
            }
        }

        private async Task EnviarComprobanteAsync()
        {
            const string email   = "qubitsoftxxi@gmail.com";
            var asunto  = LocalizationService.Get("Premium_EmailAsunto");
            var cuerpo  = LocalizationService.Get("Premium_EmailCuerpo");
            var uri = new Uri($"mailto:{email}?subject={Uri.EscapeDataString(asunto)}&body={Uri.EscapeDataString(cuerpo)}");
            try
            {
                await Launcher.OpenAsync(uri);
            }
            catch (Exception ex)
            {
                MensajeExito  = false;
                MensajeEstado = LocalizationService.GetF("Premium_ErrorCorreo", ex.Message);
            }
        }

        private async Task ActivarClaveAsync()
        {
            if (string.IsNullOrWhiteSpace(ClaveIngresada))
            {
                MensajeExito  = false;
                MensajeEstado = LocalizationService.Get("Premium_ClaveVacia");
                return;
            }

            IsBusy = true;
            MensajeEstado = string.Empty;
            try
            {
                var (ok, msg) = await _license.ActivarClaveAsync(ClaveIngresada);
                MensajeExito  = ok;
                MensajeEstado = msg;
                if (ok) ClaveIngresada = string.Empty;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RestaurarAsync()
        {
            IsBusy = true;
            MensajeEstado = string.Empty;
            try
            {
                var (ok, msg) = await _license.RestaurarCompraAsync();
                MensajeExito  = ok;
                MensajeEstado = msg;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
