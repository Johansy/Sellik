using Android.App;
using Android.OS;
using Android.Util;
using Android.Runtime;

[Activity(...)]
public class MainActivity : Microsoft.Maui.Controls.Platform.MauiAppCompatActivity
{
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) =>
        {
            Log.Error("CajaApp.Unhandled", args.Exception.ToString());
            // opcional: args.Handled = true; // si quieres evitar que el sistema termine el proceso
        };
    }
}using Android.App;
using Android.OS;
using Android.Util;
using Android.Runtime;

[Activity(...)]
public class MainActivity : Microsoft.Maui.Controls.Platform.MauiAppCompatActivity
{
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) =>
        {
            Log.Error("CajaApp.Unhandled", args.Exception.ToString());
            // opcional: args.Handled = true; // si quieres evitar que el sistema termine el proceso
        };
    }
}