#if ANDROID
using Android.App;
using Android.Preferences;
using Android.Runtime;
using AndroidX.AppCompat.App;
using CajaApp.Models;

namespace CajaApp.Plattform.Android
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        public override void OnCreate()
        {
            // Aplicar el modo noche ANTES de que se cree cualquier Activity,
            // incluido el Splash Screen. Este es el único lugar donde se puede
            // afectar el tema del Splash en Android.
            AppCompatDelegate.DefaultNightMode = ObtenerNightMode();
            base.OnCreate();
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

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
    }
}
#endif
