using CajaApp.Services;
using CajaApp.ViewModels;
using CajaApp.Views;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using Plugin.Maui.OCR;
using System.Globalization;

#if !ANDROID
using QuestPDF.Infrastructure;
#endif

namespace CajaApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // Inicializar SQLitePCLRaw con el provider verde (SQLite del sistema Android,
        // alineado a 16 KB) antes de cualquier acceso a base de datos.
        // Esto evita que se cargue libe_sqlite3.so, que no es compatible con Android 16.
        SQLitePCL.Batteries_V2.Init();

#if !ANDROID
        QuestPDF.Settings.License = LicenseType.Community;
#endif

        var builder = MauiApp.CreateBuilder();
        var lang = LocalizationService.Instance.CodigoIdioma;

        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo(lang);
        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(lang);

#pragma warning disable CA1416
        builder
            .UseMauiApp<App>()
            .UseOcr()
            .UseMauiCommunityToolkit()
#pragma warning restore CA1416
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf",  "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Servicios
        builder.Services.AddSingleton<SesionService>(SesionService.Instance);
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<ConfiguracionService>();
        builder.Services.AddSingleton<OCRService>();           // OCR nativo (Plugin.Maui.OCR)
        builder.Services.AddSingleton<CloudOCRService>();      // GPT-4o Vision + Google Vision
        builder.Services.AddSingleton<OCROrchestrator>();      // Selector automático de motor
        builder.Services.AddSingleton<ExportService>();
        builder.Services.AddSingleton<ImagenService>();
        builder.Services.AddSingleton<TemaService>(TemaService.Instance);

        // Páginas y ViewModels
        builder.Services.AddTransient<CajaPage>();
        builder.Services.AddTransient<CajaViewModel>();
        builder.Services.AddTransient<MovimientosPage>();
        builder.Services.AddTransient<MovimientosViewModel>();
        builder.Services.AddTransient<VoucherScannerPage>();
        builder.Services.AddTransient<VoucherScannerViewModel>();
        builder.Services.AddTransient<VoucherViewModel>();
        builder.Services.AddTransient<NotasPage>();
        builder.Services.AddTransient<NotasViewModel>();
        builder.Services.AddTransient<HistorialPage>();
        builder.Services.AddTransient<HistorialViewModel>();
        builder.Services.AddTransient<ConfiguracionPage>();
        builder.Services.AddTransient<ConfiguracionViewModel>();
        builder.Services.AddTransient<NotaEditorPage>();
        builder.Services.AddTransient<NotaDetallePage>();
        builder.Services.AddTransient<AcercaDePage>();
        builder.Services.AddTransient<EstadisticasPage>();
        builder.Services.AddTransient<EstadisticasViewModel>();
        builder.Services.AddTransient<ReporteCompletePage>();
        builder.Services.AddTransient<SesionesPage>();
        builder.Services.AddTransient<SesionesViewModel>();
        builder.Services.AddTransient<PremiumPage>();
        builder.Services.AddTransient<PremiumViewModel>();
        builder.Services.AddTransient<AppShell>();

        builder.Services.AddSingleton(LicenseService.Instance);
        builder.Services.AddSingleton(LocalizationService.Instance);

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
