using Foundation;
using UIKit;
using CajaApp.Models;

namespace CajaApp
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        [Foundation.Export("application:supportedInterfaceOrientationsForWindow:")]
        public UIInterfaceOrientationMask GetSupportedInterfaceOrientations(UIApplication application, UIWindow? forWindow)
        {
            var bloquearStr = Preferences.Get(ConfiguracionApp.Claves.BloquearOrientacion, "true");
            bool bloquear = !string.Equals(bloquearStr, "false", StringComparison.OrdinalIgnoreCase);
            Platforms.iOS.OrientationHelper.BloquearPortrait = bloquear;

            return bloquear
                ? UIInterfaceOrientationMask.Portrait
                : UIInterfaceOrientationMask.All;
        }
    }
}
