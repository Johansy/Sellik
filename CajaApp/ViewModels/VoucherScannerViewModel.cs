using System.Threading.Tasks;
using CajaApp.Services;
using Microsoft.Maui.Controls;
using CajaApp.Views;

public class VoucherScannerViewModel
{
    public async Task StartScannerAsync()
    {
        var granted = await PermissionsService.RequestCameraAsync();
        if (!granted)
        {
            // Mostrar alerta y ofrecer abrir ajustes
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

        // Aquí llama a la página / servicio que abre la cámara o scanner
        await Shell.Current.GoToAsync(nameof(VoucherScannerPage));
    }
}