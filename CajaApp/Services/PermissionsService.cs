using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;

namespace CajaApp.Services
{
    public static class PermissionsService
    {
        public static async Task<bool> RequestCameraAsync()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status == PermissionStatus.Granted)
                return true;

            status = await Permissions.RequestAsync<Permissions.Camera>();
            return status == PermissionStatus.Granted;
        }

        public static async Task OpenAppSettingsAsync()
        {
            // Abrir ajustes de la app (para que el usuario habilite manualmente)
            await Launcher.OpenAsync(new System.Uri("app-settings:"));
        }
    }
}