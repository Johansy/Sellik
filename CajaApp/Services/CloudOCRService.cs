using CajaApp.Models;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CajaApp.Services
{
    public class CloudOCRService
    {
        private readonly OCRService _ocrLocal;
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

        // Prompt optimizado para tickets y vouchers mexicanos.
        // Incluye la corrección S→$ que ya conocemos del OCR nativo.
        private const string PROMPT_VOUCHER = """
            Eres un extractor de datos de tickets y vouchers de pago mexicanos.
            Analiza la imagen y extrae los datos. Responde ÚNICAMENTE con JSON válido,
            sin texto adicional, sin bloques de código markdown.

            Formato requerido:
            {
              "comercio": "nombre del negocio (no el nombre del dueño)",
              "fecha": "dd/MM/yyyy HH:mm:ss  — vacío si no se ve",
              "tipo_pago": "Efectivo | Credito | Debito | Transferencia | Otro",
              "subtotal": 0.00,
              "iva": 0.00,
              "descuento": 0.00,
              "total": 0.00,
              "ultimos_digitos": "4 dígitos de la tarjeta o vacío",
              "numero_autorizacion": "código de autorización o vacío",
              "referencia": "referencia, folio o vacío",
              "numero_voucher": "número de ticket o venta o vacío",
              "texto_completo": "todo el texto visible en la imagen"
            }

            Reglas importantes:
            - Si ves S373.1 o s373.1 probablemente es $373.10 (el OCR confunde $ con S/s)
            - El campo "total" es el monto final a pagar: el que dice TOTAL, GRAN TOTAL o TOTAL A PAGAR
            - Si hay descuento, el "total" puede ser MENOR que el "subtotal" — NO uses el monto más alto
            - El "subtotal" es el monto ANTES de descuentos e impuestos
            - El "descuento" es el ahorro aplicado (ponlo como número positivo aunque aparezca con "-" en el ticket)
            - El "comercio" es el nombre del negocio, NO el nombre del dueño ni el RFC
            - Los montos van como número decimal (373.10 no como string)
            - tipo_pago: detecta Efectivo, Crédito/Credito, Débito/Debito, Transferencia, SPEI
            - Si no encuentras un dato pon vacío "" o 0
            - Ignora las líneas que son sólo guiones, iguales, puntos o caracteres decorativos (ej. "------", "======", "· · · ·"); no son datos
            """;

        // Prompt para extracción estructurada desde TEXTO (no imagen).
        // Usado cuando Google Vision extrae el texto y GPT lo interpreta.
        private const string PROMPT_TEXTO_VOUCHER = """
            Eres un extractor de datos de tickets y vouchers de pago mexicanos.
            El siguiente texto fue extraído de un voucher con OCR. Analízalo y extrae los datos.
            Responde ÚNICAMENTE con JSON válido, sin texto adicional, sin bloques de código markdown.

            Formato requerido:
            {
              "comercio": "nombre del negocio (no el nombre del dueño ni RFC)",
              "fecha": "dd/MM/yyyy HH:mm:ss  — vacío si no se ve",
              "tipo_pago": "Efectivo | Credito | Debito | Transferencia | Otro",
              "subtotal": 0.00,
              "iva": 0.00,
              "descuento": 0.00,
              "total": 0.00,
              "ultimos_digitos": "4 dígitos de la tarjeta o vacío",
              "numero_autorizacion": "código de autorización o vacío",
              "referencia": "referencia, folio o vacío",
              "numero_voucher": "número de ticket o venta o vacío",
              "texto_completo": "el mismo texto que recibiste, sin modificaciones"
            }

            Reglas importantes:
            - Si ves S373.1 o s373.1 probablemente es $373.10 (el OCR confunde $ con S/s)
            - El campo "total" es el monto final a pagar: el que dice TOTAL, GRAN TOTAL o TOTAL A PAGAR
            - Si hay descuento, el "total" puede ser MENOR que el "subtotal" — NO uses el monto más alto
            - El "subtotal" es el monto ANTES de descuentos e impuestos
            - El "descuento" es el ahorro aplicado (ponlo como número positivo aunque aparezca con "-" en el ticket)
            - El "comercio" es el nombre del negocio, NO el nombre del dueño ni el RFC
            - Los montos van como número decimal (373.10 no como string)
            - tipo_pago: detecta Efectivo, Crédito/Credito, Débito/Debito, Transferencia, SPEI
            - Si no encuentras un dato pon vacío "" o 0
            - Ignora las líneas que son sólo guiones, iguales, puntos o caracteres decorativos (ej. "------", "======", "· · · ·"); no son datos

            Texto del voucher:
            """;

        public CloudOCRService()
        {
            _ocrLocal = new OCRService();
        }

        // ── GPT-4o Vision ─────────────────────────────────────────────────────────
        public async Task<(Voucher voucher, string textoOCR)> ProcesarConGptAsync(
            byte[] imagenBytes, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException(
                    "API key de OpenAI no configurada.\n" +
                    "Obtén una en: https://platform.openai.com/api-keys");

            var base64   = Convert.ToBase64String(imagenBytes);
            var mimeType = DetectarMimeType(imagenBytes);

            // Payload de la API de OpenAI con visión
            var payload = new
            {
                model      = "gpt-4o",
                max_tokens = 1000,
                messages   = new object[]
                {
                    new { role = "system", content = PROMPT_VOUCHER },
                    new
                    {
                        role    = "user",
                        content = new object[]
                        {
                            new
                            {
                                type      = "image_url",
                                image_url = new
                                {
                                    url    = $"data:{mimeType};base64,{base64}",
                                    detail = "auto"  // auto: GPT elige la resolución óptima (más rápido que high)
                                }
                            }
                        }
                    }
                }
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            HttpResponseMessage respuesta;
            try   { respuesta = await _http.SendAsync(request); }
            catch (TaskCanceledException)
            { throw new Exception("Tiempo de espera agotado. Verifica tu conexión a internet."); }

            var respJson = await respuesta.Content.ReadAsStringAsync();
            if (!respuesta.IsSuccessStatusCode)
                throw new Exception($"Error OpenAI ({(int)respuesta.StatusCode}): {ExtraerError(respJson)}");

            var doc    = JsonDocument.Parse(respJson);
            var texto  = doc.RootElement
                            .GetProperty("choices")[0]
                            .GetProperty("message")
                            .GetProperty("content")
                            .GetString() ?? "";

            System.Diagnostics.Debug.WriteLine($"[GPT-4o] Respuesta:\n{texto}");

            var voucher = ParsearJsonGpt(texto, imagenBytes);
            return (voucher, voucher.TextoCompleto ?? texto);
        }

        // ── Google Cloud Vision ───────────────────────────────────────────────────
        public async Task<(Voucher voucher, string textoOCR)> ProcesarConGoogleVisionAsync(
            byte[] imagenBytes, string apiKey, string? gptApiKey = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException(
                    "API key de Google Cloud no configurada.\n" +
                    "Actívala en: console.cloud.google.com → APIs → Vision API");

            var base64 = Convert.ToBase64String(imagenBytes);

            var payload = new
            {
                requests = new[]
                {
                    new
                    {
                        image    = new { content = base64 },
                        features = new[] { new { type = "DOCUMENT_TEXT_DETECTION", maxResults = 1 } },
                        imageContext = new { languageHints = new[] { "es", "es-MX" } }
                    }
                }
            };

            var url = $"https://vision.googleapis.com/v1/images:annotate?key={apiKey}";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            HttpResponseMessage respuesta;
            try   { respuesta = await _http.SendAsync(request); }
            catch (TaskCanceledException)
            { throw new Exception("Tiempo de espera agotado. Verifica tu conexión a internet."); }

            var respJson = await respuesta.Content.ReadAsStringAsync();
            if (!respuesta.IsSuccessStatusCode)
                throw new Exception($"Error Google Vision ({(int)respuesta.StatusCode}): {ExtraerError(respJson)}");

            var doc = JsonDocument.Parse(respJson);

            // fullTextAnnotation contiene el texto con layout completo
            var responses = doc.RootElement.GetProperty("responses")[0];
            if (!responses.TryGetProperty("fullTextAnnotation", out var fullText))
                throw new Exception(
                    "Google Vision no pudo extraer texto de la imagen.\n" +
                    "Asegúrate de que la imagen sea nítida y bien iluminada.");

            var texto = fullText.GetProperty("text").GetString() ?? "";
            System.Diagnostics.Debug.WriteLine($"[Google Vision] Texto extraído:\n{texto}");

            // Si hay API key de OpenAI, usar GPT-4o para extraer campos estructurados del texto.
            // Esto da resultados comparables a enviar la imagen directamente a GPT-4o.
            if (!string.IsNullOrWhiteSpace(gptApiKey))
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[Google+GPT] Enviando texto a GPT-4o para extracción estructurada...");
                    var voucherGpt = await ExtraerEstructuraConGptTextAsync(texto, gptApiKey, imagenBytes);
                    return (voucherGpt, texto);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Google+GPT] Fallo en GPT: {ex.Message} → parser local");
                }
            }

            // Fallback: parser heurístico local con el texto de alta calidad de Google
            var voucher = _ocrLocal.ProcesarTextoVoucher(texto, imagenBytes);
            return (voucher, texto);
        }

        // ── Extracción de texto puro con Google Vision ────────────────────────────
        public async Task<string> ExtraerTextoGoogleVisionAsync(byte[] imagenBytes, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("API key de Google Cloud no configurada.");

            var base64 = Convert.ToBase64String(imagenBytes);
            var payload = new
            {
                requests = new[]
                {
                    new
                    {
                        image    = new { content = base64 },
                        features = new[] { new { type = "DOCUMENT_TEXT_DETECTION", maxResults = 1 } },
                        imageContext = new { languageHints = new[] { "es", "es-MX" } }
                    }
                }
            };

            var url = $"https://vision.googleapis.com/v1/images:annotate?key={apiKey}";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            HttpResponseMessage respuesta;
            try   { respuesta = await _http.SendAsync(request); }
            catch (TaskCanceledException)
            { throw new Exception("Tiempo de espera agotado. Verifica tu conexión a internet."); }

            var respJson = await respuesta.Content.ReadAsStringAsync();
            if (!respuesta.IsSuccessStatusCode)
                throw new Exception($"Error Google Vision ({(int)respuesta.StatusCode}): {ExtraerError(respJson)}");

            var doc       = JsonDocument.Parse(respJson);
            var responses = doc.RootElement.GetProperty("responses")[0];
            if (!responses.TryGetProperty("fullTextAnnotation", out var fullText))
                throw new Exception("Google Vision no pudo extraer texto de la imagen.");

            return fullText.GetProperty("text").GetString() ?? "";
        }

        // ── Combinación: texto ya extraído → GPT-4o ──────────────────────────────
        public async Task<(Voucher voucher, string textoOCR)> ProcesarTextoConGptAsync(
            string texto, string gptApiKey, byte[] imagenBytes)
        {
            var voucher = await ExtraerEstructuraConGptTextAsync(texto, gptApiKey, imagenBytes);
            return (voucher, texto);
        }

        // ── Extracción estructurada texto → GPT-4o ────────────────────────────────

        private async Task<Voucher> ExtraerEstructuraConGptTextAsync(
            string texto, string gptApiKey, byte[] imagenBytes)
        {
            var payload = new
            {
                model      = "gpt-4o",
                max_tokens = 800,
                messages   = new[]
                {
                    new { role = "system", content = PROMPT_TEXTO_VOUCHER },
                    new { role = "user",   content = texto }
                }
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gptApiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            HttpResponseMessage respuesta;
            try   { respuesta = await _http.SendAsync(request); }
            catch (TaskCanceledException)
            { throw new Exception("Tiempo de espera agotado. Verifica tu conexión a internet."); }

            var respJson = await respuesta.Content.ReadAsStringAsync();
            if (!respuesta.IsSuccessStatusCode)
                throw new Exception($"Error OpenAI GPT ({(int)respuesta.StatusCode}): {ExtraerError(respJson)}");

            var doc      = JsonDocument.Parse(respJson);
            var jsonResp = doc.RootElement
                              .GetProperty("choices")[0]
                              .GetProperty("message")
                              .GetProperty("content")
                              .GetString() ?? "";

            System.Diagnostics.Debug.WriteLine($"[Google+GPT] Respuesta:\n{jsonResp}");

            return ParsearJsonGpt(jsonResp, imagenBytes);
        }

        // ── Parsear JSON de GPT-4o ────────────────────────────────────────────────

        private Voucher ParsearJsonGpt(string textoModelo, byte[] imagenBytes)
        {
            // Extracción robusta del objeto JSON.
            // Cubre todos los formatos que GPT-4o puede devolver:
            //   • JSON puro:                  { ... }
            //   • Bloque markdown inicial:    ```json\n{ ... }\n```
            //   • Texto + bloque markdown:    "Aquí tienes...\n```json\n{ ... }\n```"
            var json = textoModelo.Trim();
            var inicioObj = json.IndexOf('{');
            var finObj    = json.LastIndexOf('}');
            if (inicioObj >= 0 && finObj > inicioObj)
                json = json[inicioObj..(finObj + 1)];
            json = json.Trim();

            JsonNode? node = null;
            try { node = JsonNode.Parse(json); }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GPT-4o] JSON inválido: {ex.Message}. Fallback a parser local.");
                return _ocrLocal.ProcesarTextoVoucher(string.Empty, imagenBytes);
            }

            if (node == null)
                return _ocrLocal.ProcesarTextoVoucher(string.Empty, imagenBytes);

            // Extraer texto_completo ANTES de cualquier operación que pueda fallar,
            // para que el fallback use el texto del ticket y no el JSON crudo.
            var textoCompleto = node["texto_completo"]?.ToString() ?? string.Empty;

            Voucher voucher;
            try
            {
                voucher = new Voucher
                {
                    TextoCompleto  = textoCompleto,
                    FechaCreacion  = DateTime.Now,
                    Moneda         = "MXN",
                    Comercio       = LimpiarString(node["comercio"]) ?? "Comercio no identificado",
                    TipoPago       = ParsearTipoPago(node["tipo_pago"]?.ToString()),
                    UltimosDigitosTarjeta = LimpiarString(node["ultimos_digitos"]) ?? "",
                    NumeroAutorizacion    = LimpiarString(node["numero_autorizacion"]) ?? "",
                    ReferenciaBanco       = LimpiarString(node["referencia"]) ?? "",
                    NumeroVoucher         = LimpiarString(node["numero_voucher"])
                                            ?? DateTime.Now.ToString("yyyyMMddHHmmss"),
                    Subtotal   = ParsearDecimal(node["subtotal"]),
                    Impuestos  = ParsearDecimal(node["iva"]),
                    Descuentos = ParsearDecimal(node["descuento"]),
                    Total      = ParsearDecimal(node["total"]),
                    Fecha     = ParsearFecha(node["fecha"]?.ToString()),
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GPT-4o] Error construyendo Voucher: {ex.Message}. Fallback con texto del ticket.");
                return _ocrLocal.ProcesarTextoVoucher(textoCompleto, imagenBytes);
            }

            // Validaciones de consistencia
            // Con descuento: Total puede ser MENOR que Subtotal (no swapear en ese caso)
            if (voucher.Total == 0 && voucher.Subtotal > 0)
                voucher.Total = voucher.Subtotal - voucher.Descuentos + voucher.Impuestos;

            if (voucher.Subtotal == 0 && voucher.Total > 0 && voucher.Impuestos > 0)
                voucher.Subtotal = voucher.Total + voucher.Descuentos - voucher.Impuestos;

            // Swapear solo si no hay descuento que explique que Subtotal > Total
            if (voucher.Subtotal > voucher.Total && voucher.Total > 0 && voucher.Descuentos == 0)
                (voucher.Subtotal, voucher.Total) = (voucher.Total, voucher.Subtotal);

            // Si el comercio quedó vacío o es muy corto, intentar extraerlo del texto
            if (string.IsNullOrWhiteSpace(voucher.Comercio) || voucher.Comercio.Length < 3)
            {
                var fallback = _ocrLocal.ProcesarTextoVoucher(textoCompleto, imagenBytes);
                voucher.Comercio = fallback.Comercio;
            }

            System.Diagnostics.Debug.WriteLine(
                $"[GPT-4o] → Comercio: {voucher.Comercio} | Total: {voucher.Total} | " +
                $"Sub: {voucher.Subtotal} | IVA: {voucher.Impuestos} | Desc: {voucher.Descuentos} | Tipo: {voucher.TipoPago}");

            return voucher;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static string DetectarMimeType(byte[] bytes)
        {
            // Detectar por magic bytes
            if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8)
                return "image/jpeg";
            if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50)
                return "image/png";
            if (bytes.Length >= 4 && bytes[0] == 0x47 && bytes[1] == 0x49)
                return "image/gif";
            return "image/jpeg"; // default seguro para cámara
        }

        private static string? LimpiarString(JsonNode? node)
        {
            if (node == null) return null;
            if (node.GetValueKind() == JsonValueKind.Null) return null;
            var s = node.ToString()?.Trim();
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        private static decimal ParsearDecimal(JsonNode? node)
        {
            if (node == null) return 0;
            try
            {
                var kind = node.GetValueKind();
                if (kind == JsonValueKind.Null) return 0;
                if (kind == JsonValueKind.Number)
                    return node.GetValue<decimal>();

                // Puede venir como string "$373.10" o "373,10"
                var s = node.ToString().Replace("$", "").Replace(",", "").Trim();
                return decimal.TryParse(s, NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var r) ? r : 0;
            }
            catch { return 0; }
        }

        private static TipoPago ParsearTipoPago(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return TipoPago.Debito;
            var u = texto.ToUpperInvariant();
            if (u.Contains("CREDITO")  || u.Contains("CRÉDITO") || u.Contains("CREDIT"))
                return TipoPago.Credito;
            if (u.Contains("DEBITO")   || u.Contains("DÉBITO")  || u.Contains("DEBIT"))
                return TipoPago.Debito;
            if (u.Contains("EFECTIVO") || u.Contains("CASH"))
                return TipoPago.Efectivo;
            if (u.Contains("TRANSFERENCIA") || u.Contains("SPEI"))
                return TipoPago.Transferencia;
            return TipoPago.Otro;
        }

        private static DateTime ParsearFecha(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return DateTime.Now;
            var formatos = new[]
            {
                "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "dd/MM/yyyy",
                "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd",
                "MM/dd/yyyy HH:mm:ss", "MM/dd/yyyy"
            };
            foreach (var fmt in formatos)
                if (DateTime.TryParseExact(texto.Trim(), fmt,
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    return d;
            return DateTime.TryParse(texto, out var dt) ? dt : DateTime.Now;
        }

        private static string ExtraerError(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                // OpenAI: { error: { message: "..." } }
                if (doc.RootElement.TryGetProperty("error", out var err) &&
                    err.TryGetProperty("message", out var msg))
                    return msg.GetString() ?? json;
                // Google: { error: { message: "..." } }
                if (doc.RootElement.TryGetProperty("error", out var gerr) &&
                    gerr.TryGetProperty("message", out var gmsg))
                    return gmsg.GetString() ?? json;
            }
            catch { }
            return json.Length > 200 ? json[..200] + "..." : json;
        }
    }
}
