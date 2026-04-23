// (Inserta en el método CreateMauiApp justo después de construir el host/app)
using System.Diagnostics;

AppDomain.CurrentDomain.UnhandledException += (s, e) =>
{
    Debug.WriteLine("[UnhandledException] " + e.ExceptionObject?.ToString());
};

TaskScheduler.UnobservedTaskException += (s, e) =>
{
    Debug.WriteLine("[UnobservedTaskException] " + e.Exception?.ToString());
};