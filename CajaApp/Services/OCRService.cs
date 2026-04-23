// Servicio OCR con extracción heurística para vouchers y tickets de caja mexicanos.
//
// Estrategias clave:
//  1. Preprocesa el texto corrigiendo errores comunes del OCR (S/s leído como $, etc.)
//  2. Extrae TODOS los montos con 1 o 2 decimales
//  3. Busca Total por etiqueta; si falla, usa el monto más alto del documento
//  4. Detecta el nombre del comercio usando el RFC como ancla (2 líneas antes)
//     o bien por heurística de las primeras líneas significativas
//
using CajaApp.Models;
using Plugin.Maui.OCR;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CajaApp.Services
{
    public class OCRService
    {
        private static readonly string[] _keywordsDescartar = new[]
        {
            "BANCO", "BANK", "TERMINAL", "FECHA", "DATE", "TARJETA", "CARD",
            "TIPO", "TYPE", "OPERACION", "TRANSACCION", "TRANSACTION",
            "SUBTOTAL", "TOTAL", "IVA", "IMPUESTO", "TAX", "DESCUENTO",
            "AUTORIZACION", "AUTHORIZATION", "AUT", "AUTH", "REFERENCIA",
            "REF", "FOLIO", "TICKET", "VOUCHER", "COMPROBANTE", "RECIBO",
            "GRACIAS", "THANK", "CONSERVE", "APROBADO", "APPROVED",
            "DEBITO", "CREDITO", "EFECTIVO", "CASH", "VENTA", "SALE",
            "VISA", "MASTERCARD", "AMEX", "SPEI", "CIE", "CLABE",
            "DETALLE", "DESCRIPCION", "CANT.", "P.UNIT", "IMPORTE",
            "TURNO", "CAJERO", "TOIS", "COLONIA", "CALLE", "CP:",
            "PAGO", "CAMBIO", "FORMA"
        };

        // ── OCR ──────────────────────────────────────────────────────────────────

        public async Task<string> ExtraerTextoDeImagenAsync(byte[] imagenBytes)
        {
            if (imagenBytes == null || imagenBytes.Length == 0)
                throw new ArgumentException("La imagen está vacía.");

            try
            {
                var ocrPlugin = OcrPlugin.Default;
                OcrResult resultado;
                try
                {
                    resultado = await ocrPlugin.RecognizeTextAsync(imagenBytes,
                        new OcrOptions.Builder().SetTryHard(true).SetLanguage("es").Build());
                }
                catch
                {
                    resultado = await ocrPlugin.RecognizeTextAsync(imagenBytes,
                        new OcrOptions.Builder().SetTryHard(true).Build());
                }

                if (!resultado.Success || string.IsNullOrWhiteSpace(resultado.AllText))
                    throw new Exception(
                        "No se pudo extraer texto. Intenta con mejor iluminación o más cerca del voucher.");

                return resultado.AllText;
            }
            catch (Exception ex) when (ex.Message.Contains("No se pudo"))
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error en OCR: {ex.Message}", ex);
            }
        }

        // ── Parsing principal ─────────────────────────────────────────────────────

        public Voucher ProcesarTextoVoucher(string textoOCR, byte[]? imagenBytes = null)
        {
            if (string.IsNullOrWhiteSpace(textoOCR))
                throw new ArgumentException("El texto OCR está vacío.");

            // ── Paso 1: corregir errores comunes del OCR ────────────────────────
            var textoProcesado = PreProcesarTexto(textoOCR);

            var lineas = textoProcesado
                .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .Where(l => !EsLineaDecorativa(l))   // ignorar separadores horizontales
                .ToList();

            var voucher = new Voucher
            {
                TextoCompleto = textoOCR,   // guardamos el original, no el preprocesado
                Fecha = DateTime.Now,
                Moneda = "MXN"
            };

            // ── Paso 2: extraer todos los montos del texto ──────────────────────
            var todosLosMontos = ExtraerTodosLosMontos(textoProcesado);

            // ── Paso 3: buscar Total, Subtotal e IVA por etiqueta ───────────────
            voucher.Total     = BuscarMontoConEtiqueta(lineas,
                new[] { "TOTAL", "IMPORTE", "AMOUNT", "A COBRAR", "TOTAL A PAGAR", "GRAN TOTAL", "TOTAL VENTA" },
                new[] { "SUBTOTAL", "SUB TOTAL", "SUB-TOTAL" });
            voucher.Subtotal  = BuscarMontoConEtiqueta(lineas, "SUBTOTAL", "SUB TOTAL", "SUB-TOTAL");
            voucher.Impuestos = BuscarMontoConEtiqueta(lineas, "IVA", "IMPUESTO", "TAX", "I.V.A");
            voucher.Descuentos = BuscarDescuento(lineas);

            // ── Paso 4: si no encontramos Total por etiqueta → monto más alto ───
            if (voucher.Total == 0 && todosLosMontos.Count > 0)
            {
                // Si ya tenemos Subtotal, no usar ese mismo valor como Total
                // (evita que Total = Subtotal cuando el ticket no tiene etiqueta TOTAL)
                var candidatos = voucher.Subtotal > 0
                    ? todosLosMontos.Where(m => m != voucher.Subtotal).ToList()
                    : todosLosMontos;

                voucher.Total = candidatos.Count > 0 ? candidatos.Max() : todosLosMontos.Max();
                System.Diagnostics.Debug.WriteLine(
                    $"[OCR] Total por heurística (máximo): {voucher.Total}");
            }

            // ── Paso 5: calcular lo que falte ───────────────────────────────────
            if (voucher.Subtotal == 0 && voucher.Total > 0 && voucher.Impuestos > 0)
                voucher.Subtotal = voucher.Total + voucher.Descuentos - voucher.Impuestos;

            // Si hay descuento pero no subtotal ni impuestos, inferir el subtotal bruto
            // Ej. Soriana: Total=149.90, Descuento=70.10 → Subtotal bruto=220.00
            if (voucher.Subtotal == 0 && voucher.Total > 0 && voucher.Descuentos > 0 && voucher.Impuestos == 0)
                voucher.Subtotal = voucher.Total + voucher.Descuentos;

            // Swapear solo si no hay descuento que explique que Subtotal > Total
            if (voucher.Subtotal > voucher.Total && voucher.Total > 0 && voucher.Descuentos == 0)
                (voucher.Subtotal, voucher.Total) = (voucher.Total, voucher.Subtotal);

            // ── Paso 6: nombre del comercio ─────────────────────────────────────
            voucher.Comercio = ExtraerComercio(lineas, textoProcesado);

            // ── Paso 7: resto de campos ─────────────────────────────────────────
            voucher.TipoPago              = ExtraerTipoPago(textoProcesado);
            voucher.UltimosDigitosTarjeta = ExtraerUltimosDigitos(textoProcesado);
            voucher.NumeroAutorizacion    = BuscarValorConEtiqueta(textoProcesado,
                                               "AUT", "AUTORIZACION", "AUTHORIZATION", "APROBACION")
                                           ?? string.Empty;
            voucher.ReferenciaBanco       = BuscarValorConEtiqueta(textoProcesado,
                                               "REF", "REFERENCIA", "REFERENCE", "FOLIO")
                                           ?? string.Empty;
            voucher.NumeroVoucher         = BuscarValorConEtiqueta(textoProcesado,
                                               "VOUCHER", "TICKET", "VENTA", "FOLIO", "TERMINAL")
                                            ?? DateTime.Now.ToString("yyyyMMddHHmmss");
            voucher.Fecha                 = ExtraerFecha(textoProcesado);

            System.Diagnostics.Debug.WriteLine(
                $"[OCR] → Comercio: {voucher.Comercio} | Total: {voucher.Total} | " +
                $"Sub: {voucher.Subtotal} | IVA: {voucher.Impuestos} | Desc: {voucher.Descuentos} | Tipo: {voucher.TipoPago}");

            return voucher;
        }

        // ── Preprocesamiento de texto ─────────────────────────────────────────────
        /// Corrige errores comunes del OCR antes de hacer el parsing.
        private string PreProcesarTexto(string texto)
        {
            // ERROR MUY FRECUENTE: OCR lee '$' como 'S' o 's'
            // Solo aplica cuando el número tiene parte decimal explícita (contexto monetario):
            //   S373.10 → $373.10  |  s42.90 → $42.90  |  S1,234.56 → $1,234.56
            // Falsos positivos evitados sin decimal: S7 ELEVEN, SUCURSAL 3, SECTOR 5
            var resultado = Regex.Replace(texto,
                @"(?<![A-Za-záéíóúñÁÉÍÓÚÑ])[Ss](\d{1,3}(?:[.,]\d{3})*[.,]\d{1,2}|\d+[.,]\d{1,2})",
                @"$$$1");

            // ERROR: OCR lee '0' como 'O' en contexto numérico (solo cuando está entre dígitos)
            // Ejemplo: 38O.1O → 380.10 (muy peligroso activar globalmente, solo entre dígitos)
            resultado = Regex.Replace(resultado, @"(\d)[Oo](\d)", "$1" + "0" + "$2");

            // ERROR: 'l' o 'I' leído como '1' en cantidades — demasiado arriesgado activarlo
            // Lo dejamos comentado para no afectar nombres

            return resultado;
        }

        // ── Detección de líneas decorativas ────────────────────────────────────

        /// Devuelve true para líneas que son separadores decorativos del ticket
        /// (ej. "------", "======", "· · · ·", "______", "* * *", "- - -").
        /// Estas líneas rompen la búsqueda de monto cuando están entre la etiqueta
        /// TOTAL y el importe que aparece en la línea siguiente.
 
        private static bool EsLineaDecorativa(string linea)
        {
            // Quitar espacios y caracteres repetidos de relleno
            var s = linea.Trim();
            if (s.Length < 3) return false;

            // Línea compuesta sólo por uno o más de estos caracteres (con posibles espacios)
            return Regex.IsMatch(s, @"^[-=_*·.~+#|\\/ ]{3,}$");
        }

        // ── Extracción de montos ─────────────────────────────────────────────

        private List<decimal> ExtraerTodosLosMontos(string texto)
        {
            var montos = new List<decimal>();

            // Patrón amplio: cubre 1 O 2 decimales, con o sin signo $, con o sin separador de miles
            // Ejemplos: $150.00 | $373.1 | 1,234.56 | 42.9 | MX$100.00
            var matches = Regex.Matches(texto,
                @"(?:MX\$|\$)?\s*(\d{1,3}(?:[,\.]\d{3})*[,\.]\d{1,2}|\d{2,}[,\.]\d{1,2})",
                RegexOptions.IgnoreCase);

            foreach (Match m in matches)
            {
                var monto = ParseMonto(m.Groups[1].Value);
                if (monto > 0.50m && monto < 1_000_000m)   // filtrar centavos sueltos y valores absurdos
                    montos.Add(monto);
            }

            return montos.Distinct().ToList();
        }

        /// Busca un monto en líneas que contengan alguna de las etiquetas.
        /// Usa word-boundary regex para que "TOTAL" no coincida dentro de "SUBTOTAL".
        /// Busca en la misma línea y hasta 3 líneas siguientes.
        private decimal BuscarMontoConEtiqueta(List<string> lineas, params string[] etiquetas)
            => BuscarMontoConEtiqueta(lineas, etiquetas, null);

        /// Sobrecarga que acepta un listado de cadenas cuya presencia en la línea la descarta,
        /// evitando que "SUB TOTAL" sea capturado al buscar "TOTAL".
        private decimal BuscarMontoConEtiqueta(List<string> lineas, string[] etiquetas, string[]? excluirSiContiene)
        {
            for (int i = 0; i < lineas.Count; i++)
            {
                var lineaUpper = lineas[i].ToUpperInvariant();
                if (!etiquetas.Any(e =>
                    Regex.IsMatch(lineaUpper, @"(?<![A-Z])" + Regex.Escape(e) + @"(?![A-Z])"))) continue;

                // Saltar líneas que contengan etiquetas conflictivas (ej. SUBTOTAL al buscar TOTAL)
                if (excluirSiContiene != null && excluirSiContiene.Any(ex => lineaUpper.Contains(ex))) continue;

                // Intentar en la misma línea primero
                var monto = PrimerMonto(lineas[i]);
                if (monto > 0) return monto;

                // Buscar en las siguientes 3 líneas (en tickets el monto puede estar abajo)
                for (int j = i + 1; j < Math.Min(lineas.Count, i + 4); j++)
                {
                    // Parar si encontramos otra etiqueta (significa que pasamos a otro campo)
                    var siguienteUpper = lineas[j].ToUpperInvariant();
                    if (etiquetas.Any(e =>
                        Regex.IsMatch(siguienteUpper, @"(?<![A-Z])" + Regex.Escape(e) + @"(?![A-Z])"))) break;
                    if (new[] { "CAMBIO", "PAGO", "EFECTIVO", "TARJETA" }
                        .Any(k => siguienteUpper.Contains(k))) break;

                    monto = PrimerMonto(lineas[j]);
                    if (monto > 0) return monto;
                }
            }
            return 0;
        }

        // Keywords de RESUMEN de ahorro (línea de totales): tienen prioridad sobre los de ítem individual.
        private static readonly string[] _etiquetasDescuentoResumen =
        {
            "AHORRASTE", "AHORRO TOTAL", "TOTAL AHORRADO",
            "TOTAL DESCUENTOS", "TOTAL DESCUENTO", "DESCUENTO TOTAL",
            "MONTO AHORRADO", "BENEFICIO TOTAL"
        };

        // Variantes de etiquetas de descuento reconocidas en tickets mexicanos.
        // Se declaran aquí para reutilizarlas en ambas estrategias sin instanciar arrays en cada llamada.
        private static readonly string[] _etiquetasDescuento =
        {
            // Palabra completa y plurales (antes "DESCUENTO" no matcheaba "DESCUENTOS" por el guard (?![A-Z]))
            "DESCUENTOS", "DESCUENTO", "DESCTO", "DSCTO", "DCTO", "DSCT",
            "DESC.", "AHORRO", "BONIFICACION", "BONIF",
            "DTO.", "DTO", "PROMO", "PROMOCION",
            "CUPON", "CUP\u00d3N", "BENEFICIO", "OFERTA", "REBAJA"
        };

        /// Busca el monto de descuento con cuatro estrategias en cascada:
        ///  0. Keywords de RESUMEN de ahorro (AHORRASTE, AHORRO TOTAL…) — mayor prioridad.
        ///  1. Etiqueta estándar en la misma línea o hasta 3 líneas siguientes.
        ///  2. Monto negativo/entre paréntesis en cualquier línea con keyword de descuento.
        ///  3. Línea que empieza directamente con "-$XX.XX" sin etiqueta (descuento inline).
        /// Devuelve siempre el valor absoluto (positivo).
        private decimal BuscarDescuento(List<string> lineas)
        {
            // ── Estrategia 0: keywords de RESUMEN de ahorro (mayor prioridad) ────
            // Cubre: "AHORRASTE $10.00", "AHORRO TOTAL $-70.10", "TOTAL DESCUENTOS 50.00"
            // Usa Contains para no depender del guard de word-boundary.
            foreach (var linea in lineas)
            {
                var u = linea.ToUpperInvariant();
                if (!_etiquetasDescuentoResumen.Any(k => u.Contains(k))) continue;

                // Buscar primero monto negativo o entre paréntesis, luego cualquier monto positivo
                var mNeg = Regex.Match(linea,
                    @"[-\(]\s*\$?\s*(\d{1,3}(?:[,\.]\d{3})*[,\.]\d{0,2}|\d+[,\.]\d{1,2})");
                if (mNeg.Success)
                {
                    var val = ParseMonto(mNeg.Groups[1].Value);
                    if (val > 0) return val;
                }

                // "AHORRASTE $10.00" — el valor viene directamente (positivo)
                var mPos = Regex.Match(linea,
                    @"\$?\s*(\d{1,3}(?:[,\.]\d{3})*[,\.]\d{1,2}|\d+[,\.]\d{1,2})");
                if (mPos.Success)
                {
                    var val = ParseMonto(mPos.Groups[1].Value);
                    if (val > 0) return val;
                }
            }

            // ── Estrategia 1: buscar por etiqueta con PrimerMonto ─────────────────
            var monto = BuscarMontoConEtiqueta(lineas, _etiquetasDescuento);
            if (monto > 0) return monto;

            // ── Estrategia 2: monto negativo/paréntesis en la misma línea ─────────
            // Cubre: "DESCUENTO -$20.00", "AHORRO (15.00)", "DSCTO -30.00"
            // Usamos Contains para no depender del guard (?![A-Z]) y capturar plurales
            foreach (var linea in lineas)
            {
                var u = linea.ToUpperInvariant();
                if (!_etiquetasDescuento.Any(k => u.Contains(k.TrimEnd('.')))) continue;

                var m = Regex.Match(linea,
                    @"[-\(]\s*\$?\s*(\d{1,3}(?:[,\.]\d{3})*[,\.]\d{0,2}|\d+[,\.]\d{0,2}|\d{2,})");
                if (m.Success)
                {
                    var val = ParseMonto(m.Groups[1].Value);
                    if (val > 0) return val;
                }
            }

            // ── Estrategia 3: línea que empieza con monto negativo sin etiqueta ───
            // Algunos POS muestran el descuento solo como "-$20.00" en una línea propia
            var excluirNegativo = new[] { "TOTAL", "SUBTOTAL", "IVA", "IMPUESTO",
                                           "CAMBIO", "PAGO", "EFECTIVO", "DEVOLUCION", "SALDO" };
            foreach (var linea in lineas)
            {
                var trimmed = linea.Trim();
                var u = trimmed.ToUpperInvariant();
                if (excluirNegativo.Any(k => u.Contains(k))) continue;

                var m = Regex.Match(trimmed,
                    @"^-\s*\$?\s*(\d{1,3}(?:[,\.]\d{3})*[,\.]\d{1,2}|\d+[,\.]\d{1,2})");
                if (m.Success)
                {
                    var val = ParseMonto(m.Groups[1].Value);
                    if (val > 0) return val;
                }
            }

            return 0;
        }

        private decimal PrimerMonto(string linea)
        {
            // Patrón ampliado: captura montos con o sin decimales, con o sin separador de miles.
            // Ejemplos: $373.10 | 1,234.56 | $150 | 42.9 | $5.00
            var m = Regex.Match(linea,
                @"[\$]?\s*(\d{1,3}(?:[,\.]\d{3})*[,\.]\d{1,2}|\d{2,}[,\.]\d{1,2}|\d{3,})");
            if (!m.Success) return 0;
            var monto = ParseMonto(m.Groups[1].Value);
            // Ignorar si parece folio/referencia (> 6 dígitos enteros sin decimales y > 99999)
            if (monto == Math.Floor(monto) && monto > 99_999m && !linea.Contains('.') && !linea.Contains(','))
                return 0;
            return monto;
        }

        private decimal ParseMonto(string texto)
        {
            var s = texto.Replace("$", "").Replace(" ", "").Trim();
            if (string.IsNullOrEmpty(s)) return 0;

            var ultimaComa  = s.LastIndexOf(',');
            var ultimoPunto = s.LastIndexOf('.');

            if (ultimaComa > ultimoPunto)
                s = s.Replace(".", "").Replace(",", ".");   // formato europeo: 1.234,56
            else
                s = s.Replace(",", "");                     // formato americano: 1,234.56

            return decimal.TryParse(s, NumberStyles.Any,
                CultureInfo.InvariantCulture, out var r) ? r : 0;
        }

        // ── Extracción de nombre del comercio ────────────────────────────────────

        private string ExtraerComercio(List<string> lineas, string texto)
        {
            // ESTRATEGIA 1: etiqueta explícita
            var conEtiqueta = BuscarValorConEtiqueta(texto,
                "COMERCIO", "ESTABLECIMIENTO", "TIENDA", "NEGOCIO", "RAZON SOCIAL", "RAZÓN SOCIAL");
            if (!string.IsNullOrEmpty(conEtiqueta) && conEtiqueta.Length > 2)
                return Limpiar(conEtiqueta);

            // ESTRATEGIA 2: ancla en RFC
            // En tickets mexicanos, el RFC está en el encabezado y el nombre del
            // comercio suele estar 1 o 2 líneas ANTES del RFC.
            var idxRfc = lineas.FindIndex(l =>
                Regex.IsMatch(l, @"\bRFC\b", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(l, @"[A-Z&Ñ]{3,4}\d{6}[A-Z0-9]{3}", RegexOptions.IgnoreCase));

            if (idxRfc >= 1)
            {
                // Recorrer hacia atrás desde el RFC buscando la línea más descriptiva
                for (int i = idxRfc - 1; i >= Math.Max(0, idxRfc - 4); i--)
                {
                    if (EsCandidatoComercio(lineas[i]))
                        return Limpiar(lineas[i]);
                }
            }

            // ESTRATEGIA 3: heurística de primeras líneas
            // Saltamos líneas que parezcan nombre de banco
            int inicio = lineas.Count > 0 && EsBanco(lineas[0]) ? 1 : 0;

            for (int i = inicio; i < Math.Min(lineas.Count, 12); i++)
            {
                if (EsCandidatoComercio(lineas[i]))
                    return Limpiar(lineas[i]);
            }

            return "Comercio no identificado";
        }

        private bool EsBanco(string linea)
        {
            var u = linea.ToUpperInvariant();
            return u.Contains("BANCO") || u.Contains("BANK") ||
                   new[] { "SANTANDER", "BBVA", "BANAMEX", "CITIBANAMEX",
                            "HSBC", "BANORTE", "SCOTIABANK", "INBURSA",
                            "BANCOMER", "AFIRME", "BANBAJIO" }
                   .Any(b => u.Contains(b));
        }

        private bool EsCandidatoComercio(string linea)
        {
            if (string.IsNullOrWhiteSpace(linea) || linea.Length < 3) return false;
            var u = linea.ToUpperInvariant().Trim();

            // Descartar si COMIENZA con algún keyword técnico
            if (_keywordsDescartar.Any(k =>
                u == k || u.StartsWith(k + " ") || u.StartsWith(k + ":") || u.StartsWith(k + ".")))
                return false;

            // Descartar patrones técnicos
            if (Regex.IsMatch(linea, @"^\d+$"))                    return false; // solo números
            if (Regex.IsMatch(linea, @"^\d{2}[/\-]\d{2}"))        return false; // fecha
            if (Regex.IsMatch(linea, @"^\d{2}:\d{2}"))            return false; // hora
            if (Regex.IsMatch(linea, @"^\*"))                      return false; // tarjeta enmascarada
            if (Regex.IsMatch(linea, @"^[0-9\s\-\*\#\.\$,]+$"))   return false; // solo símbolos/números
            if (Regex.IsMatch(linea, @"^\d{4}-\d{2}-\d{2}"))      return false; // fecha ISO
            if (Regex.IsMatch(linea, @"^RFC:", RegexOptions.IgnoreCase)) return false;
            if (Regex.IsMatch(linea, @"^[A-Z&Ñ]{3,4}\d{6}"))      return false; // RFC solo
            if (Regex.IsMatch(linea, @"^CALLE\b", RegexOptions.IgnoreCase)) return false;
            if (Regex.IsMatch(linea, @"^COL(ONIA)?\.?\s", RegexOptions.IgnoreCase)) return false;

            // Debe tener al menos 3 letras seguidas
            return Regex.IsMatch(linea, @"[A-ZÁÉÍÓÚÑa-záéíóúñ]{3,}");
        }

        private string Limpiar(string texto)
        {
            // Quitar caracteres extraños conservando letras, números, espacios y puntuación básica
            var s = Regex.Replace(texto, @"[^\w\sÁÉÍÓÚáéíóúÑñ#\-\./&']", "").Trim();
            // Capitalizar correctamente (Title Case)
            if (s.Length > 1 && s == s.ToUpperInvariant())
                s = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
            return s.Length > 1 ? s : texto.Trim();
        }

        // ── Otros extractores ────────────────────────────────────────────────────

        private string? BuscarValorConEtiqueta(string texto, params string[] etiquetas)
        {
            foreach (var etiqueta in etiquetas)
            {
                var m = Regex.Match(texto, etiqueta + @"\s*[:=]?\s*(.+)",
                    RegexOptions.IgnoreCase);
                if (!m.Success) continue;

                var valor = m.Groups[1].Value.Trim();
                var salto = valor.IndexOf('\n');
                if (salto > 0) valor = valor[..salto].Trim();
                if (valor.Length > 0 && !Regex.IsMatch(valor, @"^[\s\$\.\,0]+$"))
                    return valor;
            }
            return null;
        }

        private string ExtraerUltimosDigitos(string texto)
        {
            var m = Regex.Match(texto, @"(?:\*{4}|X{4})\s*(\d{4})");
            if (m.Success) return m.Groups[1].Value;
            m = Regex.Match(texto, @"TERMINAD[AO]\s*EN\s*(\d{4})", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : string.Empty;
        }

        private TipoPago ExtraerTipoPago(string texto)
        {
            var u = texto.ToUpperInvariant();

            // Los tickets de punto de venta suelen indicar "Tipo Pago: Efectivo Pesos"
            var matchTipo = Regex.Match(u, @"TIPO\s*(?:DE\s*)?PAGO\s*[:\-]?\s*(\w+)");
            if (matchTipo.Success)
            {
                var tipo = matchTipo.Groups[1].Value;
                if (tipo.Contains("EFECTIVO") || tipo.Contains("CASH")) return TipoPago.Efectivo;
                if (tipo.Contains("CREDITO")  || tipo.Contains("CREDIT")) return TipoPago.Credito;
                if (tipo.Contains("DEBITO")   || tipo.Contains("DEBIT"))  return TipoPago.Debito;
                if (tipo.Contains("TRANSFER")  || tipo.Contains("SPEI"))  return TipoPago.Transferencia;
            }

            // Fallback: buscar palabras clave en todo el texto
            if (Regex.IsMatch(u, @"CR[ÉE]DITO|CREDIT"))   return TipoPago.Credito;
            if (Regex.IsMatch(u, @"D[ÉE]BITO|DEBIT"))     return TipoPago.Debito;
            if (Regex.IsMatch(u, @"EFECTIVO|CASH|PESOS"))  return TipoPago.Efectivo;
            if (Regex.IsMatch(u, @"TRANSFERENCIA|SPEI"))   return TipoPago.Transferencia;
            return TipoPago.Debito;
        }

        private DateTime ExtraerFecha(string texto)
        {
            // Formato especial de POS mexicanos: "Fecha: 2026-04-04 Hora: 17:00:22"
            var mFechaHora = Regex.Match(texto,
                @"Fecha\s*:\s*(\d{4}[/\-]\d{2}[/\-]\d{2})\s+Hora\s*:\s*(\d{2}:\d{2}:\d{2})",
                RegexOptions.IgnoreCase);
            if (mFechaHora.Success &&
                DateTime.TryParse($"{mFechaHora.Groups[1].Value} {mFechaHora.Groups[2].Value}",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var fechaPos))
                return fechaPos;

            var patrones = new[]
            {
                // Fecha ISO con hora (común en POS modernos): 2026-04-04 17:00:22
                @"(\d{4}[/\-]\d{2}[/\-]\d{2}\s+\d{2}:\d{2}:\d{2})",
                @"(\d{4}[/\-]\d{2}[/\-]\d{2}\s+\d{2}:\d{2})",
                @"(\d{4}[/\-]\d{2}[/\-]\d{2})",
                // Fecha mexicana con hora
                @"(\d{2}[/\-]\d{2}[/\-]\d{4}\s+\d{2}:\d{2}:\d{2})",
                @"(\d{2}[/\-]\d{2}[/\-]\d{4}\s+\d{2}:\d{2})",
                @"(\d{2}[/\-]\d{2}[/\-]\d{4})"
            };
            var formatos = new[]
            {
                "yyyy-MM-dd HH:mm:ss", "yyyy/MM/dd HH:mm:ss",
                "yyyy-MM-dd HH:mm",    "yyyy/MM/dd HH:mm",
                "yyyy-MM-dd",          "yyyy/MM/dd",
                "dd/MM/yyyy HH:mm:ss", "dd-MM-yyyy HH:mm:ss",
                "dd/MM/yyyy HH:mm",    "dd-MM-yyyy HH:mm",
                "dd/MM/yyyy",          "dd-MM-yyyy"
            };

            foreach (var patron in patrones)
            {
                var m = Regex.Match(texto, patron);
                if (!m.Success) continue;
                foreach (var fmt in formatos)
                    if (DateTime.TryParseExact(m.Groups[1].Value.Trim(), fmt,
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
                        return fecha;
            }
            return DateTime.Now;
        }
    }
}
