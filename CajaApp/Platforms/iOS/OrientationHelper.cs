#if IOS || MACCATALYST
namespace CajaApp.Platforms.iOS
{
    /// <summary>
    /// Controla el bloqueo de orientación para iOS/MacCatalyst.
    /// </summary>
    public static class OrientationHelper
    {
        public static bool BloquearPortrait { get; set; } = true;
    }
}
#endif
