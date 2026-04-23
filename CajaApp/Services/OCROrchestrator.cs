// Orquestador OCR: decide qué motor usar según la configuración y el estado de red.
// Modos disponibles (ModoOCR):
//   Auto     → Nube si hay red + API key configurada, nativo si no
//   Nativo   → Siempre Plugin.Maui.OCR (offline, gratis, calidad media)
//   GPT4o    → Siempre OpenAI GPT-4o Vision (requiere red + API key)
//   Google   → Siempre Google Cloud Vision (requiere red + API key)
//
using CajaApp.Models;

namespace CajaApp.Services
{
    public class OCROrchestrator
    {
        private readonly OCRService        _nativo;
        private readonly CloudOCRService   _nube;
        private readonly ConfiguracionService _config;

        public OCROrchestrator(ConfiguracionService config)
        {
            _nativo = new OCRService();
            _nube   = new CloudOCRService();
            _config = config;
        }
        public async Task<(Voucher voucher, string textoOCR)> ProcesarAsync(byte[] imagenBytes)
        {
            var modo = await ObtenerModoAsync();

            System.Diagnostics.Debug.WriteLine($"[OCR Orquestador] Modo: {modo}");

            return modo switch
            {
                ModoOCR.GPT4o        => await ProcesarConGptAsync(imagenBytes),
                ModoOCR.Google       => await ProcesarConGoogleAsync(imagenBytes),
                ModoOCR.NativoConGPT => await ProcesarNativoConGptAsync(imagenBytes),
                ModoOCR.Fusion       => await ProcesarFusionAsync(imagenBytes),
                ModoOCR.Auto         => await ProcesarAutoAsync(imagenBytes),
                _                    => await ProcesarNativoAsync(imagenBytes)
            };
        }

        // ── Modos individuales ────────────────────────────────────────────────────

        private async Task<(Voucher, string)> ProcesarNativoAsync(byte[] imagenBytes)
        {
            var texto   = await _nativo.ExtraerTextoDeImagenAsync(imagenBytes);
            var voucher = _nativo.ProcesarTextoVoucher(texto, imagenBytes);
            return (voucher, texto);
        }

        private async Task<(Voucher, string)> ProcesarConGptAsync(byte[] imagenBytes)
        {
            var apiKey = await _config.ObtenerConfiguracionAsync(
                ConfiguracionApp.Claves.OCRApiKeyOpenAI);

            if (string.IsNullOrWhiteSpace(apiKey) && AppSecrets.OpenAIKeyConfigurada)
                apiKey = AppSecrets.GetOpenAIKey();

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException(
                    "No hay API key de OpenAI configurada.\n" +
                    "Ve a Configuración → OCR en la Nube para agregarla.");

            return await _nube.ProcesarConGptAsync(imagenBytes, apiKey);
        }

        private async Task<(Voucher, string)> ProcesarConGoogleAsync(byte[] imagenBytes)
        {
            var apiKey    = await _config.ObtenerConfiguracionAsync(
                ConfiguracionApp.Claves.OCRApiKeyGoogle);
            var gptApiKey = await _config.ObtenerConfiguracionAsync(
                ConfiguracionApp.Claves.OCRApiKeyOpenAI);

            if (string.IsNullOrWhiteSpace(apiKey) && AppSecrets.GoogleKeyConfigurada)
                apiKey = AppSecrets.GetGoogleKey();

            if (string.IsNullOrWhiteSpace(gptApiKey) && AppSecrets.OpenAIKeyConfigurada)
                gptApiKey = AppSecrets.GetOpenAIKey();

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException(
                    "No hay API key de Google Cloud configurada.\n" +
                    "Ve a Configuración → OCR en la Nube para agregarla.");

            return await _nube.ProcesarConGoogleVisionAsync(imagenBytes, apiKey, gptApiKey);
        }

        /// <summary>
        /// Nativo + GPT-4o texto:
        ///   El OCR del dispositivo extrae el texto (gratis, sin red)
        ///   y GPT-4o lo estructura como JSON (modo texto, ~10× más barato que visión).
        /// </summary>
        private async Task<(Voucher, string)> ProcesarNativoConGptAsync(byte[] imagenBytes)
        {
            var texto = await _nativo.ExtraerTextoDeImagenAsync(imagenBytes);

            var gptApiKey = await _config.ObtenerConfiguracionAsync(
                ConfiguracionApp.Claves.OCRApiKeyOpenAI);

            if (string.IsNullOrWhiteSpace(gptApiKey) && AppSecrets.OpenAIKeyConfigurada)
                gptApiKey = AppSecrets.GetOpenAIKey();

            if (string.IsNullOrWhiteSpace(gptApiKey))
                throw new InvalidOperationException(
                    "No hay API key de OpenAI configurada.\n" +
                    "Ve a Configuración → OCR en la Nube para agregarla.");

            return await _nube.ProcesarTextoConGptAsync(texto, gptApiKey, imagenBytes);
        }

        /// Modo Fusión: los tres motores combinados.
        ///   1. OCR nativo (local, gratis) y Google Vision corren en paralelo.
        ///   2. Se usa el texto con más contenido extraído.
        ///   3. GPT-4o estructura el texto final (modo texto, barato).
        ///   Si no hay keys de nube, usa el texto nativo con el parser local.
        ///   
        private async Task<(Voucher, string)> ProcesarFusionAsync(byte[] imagenBytes)
        {
            var keyGpt    = await _config.ObtenerConfiguracionAsync(ConfiguracionApp.Claves.OCRApiKeyOpenAI);
            var keyGoogle = await _config.ObtenerConfiguracionAsync(ConfiguracionApp.Claves.OCRApiKeyGoogle);

            if (string.IsNullOrWhiteSpace(keyGpt) && AppSecrets.OpenAIKeyConfigurada)
                keyGpt = AppSecrets.GetOpenAIKey();
            if (string.IsNullOrWhiteSpace(keyGoogle) && AppSecrets.GoogleKeyConfigurada)
                keyGoogle = AppSecrets.GetGoogleKey();

            // Nativo siempre (local, sin costo)
            var textoNativoTask = _nativo.ExtraerTextoDeImagenAsync(imagenBytes);

            // Google Vision en paralelo si hay red + key
            Task<string>? textoGoogleTask = null;
            if (!string.IsNullOrWhiteSpace(keyGoogle) && HayConexion())
                textoGoogleTask = _nube.ExtraerTextoGoogleVisionAsync(imagenBytes, keyGoogle);

            var textoNativo = await textoNativoTask;
            string? textoGoogle = null;
            if (textoGoogleTask != null)
            {
                try { textoGoogle = await textoGoogleTask; }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Fusion] Google Vision falló: {ex.Message}");
                }
            }

            // Tomar el texto más completo (mayor cantidad de caracteres = más información)
            var textoFinal = SeleccionarMejorTexto(textoNativo, textoGoogle);
            System.Diagnostics.Debug.WriteLine(
                $"[Fusion] Nativo: {textoNativo.Length} ch | Google: {textoGoogle?.Length ?? 0} ch → usando: {textoFinal.Length} ch");

            // Estructurar con GPT-4o si está disponible
            if (!string.IsNullOrWhiteSpace(keyGpt) && HayConexion())
            {
                try { return await _nube.ProcesarTextoConGptAsync(textoFinal, keyGpt, imagenBytes); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Fusion] GPT-4o falló: {ex.Message} → parser local");
                }
            }

            // Fallback: parser heurístico local con el mejor texto disponible
            var voucher = _nativo.ProcesarTextoVoucher(textoFinal, imagenBytes);
            return (voucher, textoFinal);
        }

        private static string SeleccionarMejorTexto(string textoA, string? textoB)
        {
            if (string.IsNullOrWhiteSpace(textoB)) return textoA;
            if (string.IsNullOrWhiteSpace(textoA)) return textoB;
            return textoB.Length > textoA.Length ? textoB : textoA;
        }


        /// Modo automático:
        ///  1. Si hay red + API key GPT → usar GPT-4o
        ///  2. Si hay red + API key Google → usar Google Vision
        ///  3. Fallback a nativo
        private async Task<(Voucher, string)> ProcesarAutoAsync(byte[] imagenBytes)
        {
            if (!HayConexion())
            {
                System.Diagnostics.Debug.WriteLine("[OCR Auto] Sin red → nativo");
                return await ProcesarNativoAsync(imagenBytes);
            }

            var keyGpt    = await _config.ObtenerConfiguracionAsync(ConfiguracionApp.Claves.OCRApiKeyOpenAI);
            var keyGoogle = await _config.ObtenerConfiguracionAsync(ConfiguracionApp.Claves.OCRApiKeyGoogle);

            if (string.IsNullOrWhiteSpace(keyGpt) && AppSecrets.OpenAIKeyConfigurada)
                keyGpt = AppSecrets.GetOpenAIKey();
            if (string.IsNullOrWhiteSpace(keyGoogle) && AppSecrets.GoogleKeyConfigurada)
                keyGoogle = AppSecrets.GetGoogleKey();

            // Preferir GPT-4o si tiene key (mejor extracción estructurada)
            if (!string.IsNullOrWhiteSpace(keyGpt))
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[OCR Auto] Con red + GPT key → GPT-4o");
                    return await _nube.ProcesarConGptAsync(imagenBytes, keyGpt);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[OCR Auto] GPT-4o falló: {ex.Message} → fallback");
                }
            }

            // Si GPT falla o no tiene key, intentar Google
            if (!string.IsNullOrWhiteSpace(keyGoogle))
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[OCR Auto] Con red + Google key → Google Vision");
                    return await _nube.ProcesarConGoogleVisionAsync(imagenBytes, keyGoogle, keyGpt);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[OCR Auto] Google Vision falló: {ex.Message} → nativo");
                }
            }

            // Fallback final: OCR nativo
            System.Diagnostics.Debug.WriteLine("[OCR Auto] Sin keys configuradas → nativo");
            return await ProcesarNativoAsync(imagenBytes);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private async Task<ModoOCR> ObtenerModoAsync()
        {
            var valor = await _config.ObtenerConfiguracionAsync(
                ConfiguracionApp.Claves.OCRModo, "0");
            return Enum.TryParse<ModoOCR>(valor, out var modo) ? modo : ModoOCR.Auto;
        }

        private static bool HayConexion()
        {
            var acceso = Connectivity.Current.NetworkAccess;
            return acceso == NetworkAccess.Internet || acceso == NetworkAccess.ConstrainedInternet;
        }

        public async Task<string> ObtenerDescripcionModoAsync()
        {
            var modo = await ObtenerModoAsync();
            var hayRed = HayConexion();

            return modo switch
            {
                ModoOCR.Auto         => hayRed ? "🤖 Auto (nube disponible)" : "🤖 Auto (sin red → nativo)",
                ModoOCR.Nativo       => "📱 Nativo (offline)",
                ModoOCR.GPT4o        => "✨ GPT-4o Vision",
                ModoOCR.Google       => "🔍 Google Cloud Vision",
                ModoOCR.NativoConGPT => hayRed ? "📱+✨ Nativo + GPT-4o texto" : "📱+✨ Nativo + GPT-4o (sin red)",
                ModoOCR.Fusion       => hayRed ? "⚡ Fusión (nativo + Google + GPT-4o)" : "⚡ Fusión (sin red → nativo)",
                _                    => "Desconocido"
            };
        }
    }

    public enum ModoOCR
    {
        Auto         = 0,   // Nube si hay red, nativo si no
        Nativo       = 1,   // Siempre local
        GPT4o        = 2,   // Siempre OpenAI GPT-4o Vision (imagen → JSON)
        Google       = 3,   // Siempre Google Cloud Vision
        NativoConGPT = 4,   // Nativo extrae texto localmente → GPT-4o estructura (~10× más barato)
        Fusion       = 5    // Nativo + Google Vision en paralelo → mejor texto → GPT-4o estructura
    }
}
