// ViewModels/VoucherScannerViewModel.cs
using System.Threading.Tasks;
using CajaApp.Services;
using CajaApp.Views;
using Microsoft.Maui.Controls;

namespace CajaApp.ViewModels
{
    public class VoucherScannerViewModel
    {
    private readonly DatabaseService _db;
    private readonly LicenseService _licenseService;

    public VoucherScannerViewModel(DatabaseService db, LicenseService licenseService)
    {
        _db = db;
        _licenseService = licenseService;
    }

    public async Task StartScannerAsync()
    {
        // Verificar límite de vouchers en plan Free
        if (!await _licenseService.PuedeEscanearVoucherAsync(_db))
        {
            var page = Application.Current?.Windows.Count > 0
                ? Application.Current.Windows[0].Page
                : null;

            var irAPremium = await (page?.DisplayAlert(
                "Límite alcanzado 🆓",
                $"El plan Gratis permite escanear hasta {_licenseService.LimiteVouchers} vouchers.\n\nActualiza a Premium para escanear ilimitado.",
                "Ver Premium",
                "Cancelar") ?? Task.FromResult(false));

            if (irAPremium)
                await Shell.Current.GoToAsync(nameof(PremiumPage));

            return;
        }

        var granted = await PermissionsService.RequestCameraAsync();
        if (!granted)
        {
            var page = Application.Current?.Windows.Count > 0
                ? Application.Current.Windows[0].Page
                : null;
            await (page?.DisplayAlert(
                "Permiso cámara",
                "La app necesita permiso para usar la cámara. Ábrir ajustes para habilitarlo.",
                "Abrir ajustes",
                "Cancelar") ?? Task.CompletedTask);

            await PermissionsService.OpenAppSettingsAsync();
            return;
        }

        await Shell.Current.GoToAsync(nameof(VoucherScannerPage));
    }
}
}
