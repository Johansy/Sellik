#if ANDROID
using Android;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Preferences;
using AndroidX.AppCompat.App;
using CajaApp.Models;

namespace CajaApp.Plattform.Android
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            int nightMode = ObtenerNightMode();

            // Seleccionar el estilo del Splash ANTES de base.OnCreate() para que
            // el fondo correcto (claro/oscuro) se muestre durante la carga inicial.
            // values-night no puede ser controlado por DefaultNightMode (responde al
            // sistema), por eso usamos dos estilos explícitos y SetTheme().
            bool oscuro = nightMode == AppCompatDelegate.ModeNightYes ||
                          (nightMode == AppCompatDelegate.ModeNightFollowSystem && EsSistemaOscuro());
            SetTheme(oscuro
                ? Resource.Style.Maui_SplashTheme_Dark
                : Resource.Style.Maui_SplashTheme_Light);

            AppCompatDelegate.DefaultNightMode = nightMode;
            base.OnCreate(savedInstanceState);
            Delegate?.SetLocalNightMode(nightMode);

            // Aplicar orientación según preferencia guardada
            var ctx1 = global::Android.App.Application.Context;
            var bloquearStr = ctx1.GetSharedPreferences(ctx1.PackageName, global::Android.Content.FileCreationMode.Private)
                ?.GetString(ConfiguracionApp.Claves.BloquearOrientacion, "true") ?? "true";
            bool bloquear = !string.Equals(bloquearStr, "false", StringComparison.OrdinalIgnoreCase);
            RequestedOrientation = bloquear
                ? global::Android.Content.PM.ScreenOrientation.Portrait
                : global::Android.Content.PM.ScreenOrientation.Unspecified;

#pragma warning disable CA1416
            RequestPermissions(new string[]
            {
                Manifest.Permission.Camera,
                Manifest.Permission.ReadExternalStorage,
                Manifest.Permission.WriteExternalStorage
            }, 0);
#pragma warning restore CA1416
        }

        private static int ObtenerNightMode()
        {
            try
            {
#pragma warning disable CS0618, CA1422
                var prefs = PreferenceManager.GetDefaultSharedPreferences(
                    global::Android.App.Application.Context);
#pragma warning restore CS0618, CA1422

                var temaStr = prefs?.GetString(ConfiguracionApp.Claves.TemaApp, "2") ?? "2";
                int.TryParse(temaStr, out int temaInt);

                return (TemaAplicacion)temaInt switch
                {
                    TemaAplicacion.Oscuro     => AppCompatDelegate.ModeNightYes,
                    TemaAplicacion.Automatico => AppCompatDelegate.ModeNightFollowSystem,
                    _                         => AppCompatDelegate.ModeNightNo
                };
            }
            catch
            {
                return AppCompatDelegate.ModeNightFollowSystem;
            }
        }

        private static bool EsSistemaOscuro()
        {
            try
            {
                var uiMode = global::Android.App.Application.Context.Resources?.Configuration?.UiMode
                             ?? global::Android.Content.Res.UiMode.TypeUndefined;
                return (uiMode & global::Android.Content.Res.UiMode.NightMask) ==
                       global::Android.Content.Res.UiMode.NightYes;
            }
            catch
            {
                return false;
            }
        }
    }
}
#endif
