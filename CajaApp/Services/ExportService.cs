// Genera archivos Excel (.xlsx) y PDF con uno o varios vouchers.
using CajaApp.Models;
using ClosedXML.Excel;
#if ANDROID
using SkiaSharp;
#else
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using IContainer = QuestPDF.Infrastructure.IContainer;
using PdfColors = QuestPDF.Helpers.Colors;
#endif
using System.Globalization;

namespace CajaApp.Services
{
    public class ExportService
    {
        private const string COLOR_MORADO      = "673AB7";
        private const string COLOR_MORADO_LITE = "EDE7F6";
        private const string COLOR_MORADO_MED  = "D1C4E9";
        private const string COLOR_FILA_PAR    = "FAFAFA";

        // ── Excel ────────────────────────────────────────────────────────────────
        /// <summary>
        /// Genera un archivo .xlsx con dos hojas: Resumen y Detalle.
        /// </summary>
        public string GenerarExcel(IEnumerable<Voucher> vouchers, string? nombreArchivo = null)
        {
            var lista = vouchers.OrderByDescending(v => v.Fecha).ToList();
            if (!lista.Any()) throw new InvalidOperationException("No hay vouchers para exportar.");

            var ruta = RutaTemporal(nombreArchivo ?? $"vouchers_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

            using var wb = new XLWorkbook();

            AgregarHojaResumen(wb, lista);
            AgregarHojaDetalle(wb, lista);

            wb.SaveAs(ruta);
            return ruta;
        }

        private void AgregarHojaResumen(XLWorkbook wb, List<Voucher> lista)
        {
            var ws = wb.Worksheets.Add("Resumen");
            ws.ShowGridLines = false;

            // Título
            var tit = ws.Range("A1:C1").Merge();
            tit.Value = "REPORTE DE VOUCHERS";
            EstiloTitulo(tit.Style);
            ws.Row(1).Height = 28;

            // Subtítulo
            var sub = ws.Range("A2:C2").Merge();
            sub.Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}";
            sub.Style.Font.Italic = true;
            sub.Style.Font.FontColor = XLColor.Gray;
            sub.Style.Font.FontSize = 9;
            sub.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Encabezado tabla resumen
            int fila = 4;
            EncabezadoCelda(ws.Cell(fila, 1), "Tipo de Pago");
            EncabezadoCelda(ws.Cell(fila, 2), "Cantidad");
            EncabezadoCelda(ws.Cell(fila, 3), "Total");
            ws.Row(fila).Height = 20;

            fila++;
            var grupos = lista.GroupBy(v => v.TipoPago)
                              .OrderBy(g => g.Key.ToString());

            int filaInicio = fila;
            foreach (var g in grupos)
            {
                DataCelda(ws.Cell(fila, 1), g.Key.ToString(), fila % 2 == 0);
                DataCelda(ws.Cell(fila, 2), g.Count(), fila % 2 == 0,
                    XLAlignmentHorizontalValues.Center);
                DataCeldaMoneda(ws.Cell(fila, 3), g.Sum(v => v.Total), fila % 2 == 0);
                fila++;
            }

            // Fila de total general
            var celdaTipoTotal = ws.Cell(fila, 1);
            celdaTipoTotal.Value = "TOTAL GENERAL";
            EstiloTotal(celdaTipoTotal.Style);

            var celdaCantTotal = ws.Cell(fila, 2);
            celdaCantTotal.FormulaA1 = $"=SUM(B{filaInicio}:B{fila - 1})";
            EstiloTotal(celdaCantTotal.Style);
            celdaCantTotal.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var celdaMontoTotal = ws.Cell(fila, 3);
            celdaMontoTotal.FormulaA1 = $"=SUM(C{filaInicio}:C{fila - 1})";
            EstiloTotal(celdaMontoTotal.Style);
            celdaMontoTotal.Style.NumberFormat.Format = "$#,##0.00";

            fila += 2;
            ws.Cell(fila, 1).Value = "Total de vouchers";
            ws.Cell(fila, 1).Style.Font.Bold = true;
            ws.Cell(fila, 2).Value = lista.Count;

            // Ajustar columnas
            ws.Column(1).Width = 20;
            ws.Column(2).Width = 12;
            ws.Column(3).Width = 16;
        }

        private void AgregarHojaDetalle(XLWorkbook wb, List<Voucher> lista)
        {
            var ws = wb.Worksheets.Add("Detalle");
            ws.ShowGridLines = false;

            string[] encabezados = { "#", "Fecha", "Comercio", "Tipo Pago", "Tarjeta",
                                     "Folio/Ticket", "Subtotal", "IVA", "Total", "Autorización", "Notas" };
            double[] anchos     = { 5, 18, 32, 13, 11, 14, 13, 11, 13, 14, 20 };

            for (int i = 0; i < encabezados.Length; i++)
            {
                EncabezadoCelda(ws.Cell(1, i + 1), encabezados[i]);
                ws.Column(i + 1).Width = anchos[i];
            }
            ws.Row(1).Height = 22;

            for (int i = 0; i < lista.Count; i++)
            {
                var v      = lista[i];
                int fila   = i + 2;
                bool esPar = fila % 2 == 0;

                DataCelda(ws.Cell(fila, 1), i + 1, esPar, XLAlignmentHorizontalValues.Center);
                DataCelda(ws.Cell(fila, 2), v.Fecha.ToString("dd/MM/yyyy HH:mm"), esPar);
                DataCelda(ws.Cell(fila, 3), v.Comercio, esPar);
                DataCelda(ws.Cell(fila, 4), v.TipoPago.ToString(), esPar);
                DataCelda(ws.Cell(fila, 5),
                    string.IsNullOrEmpty(v.UltimosDigitosTarjeta) ? "-" : $"****{v.UltimosDigitosTarjeta}",
                    esPar, XLAlignmentHorizontalValues.Center);
                DataCelda(ws.Cell(fila, 6), v.NumeroVoucher ?? "-", esPar);
                DataCeldaMoneda(ws.Cell(fila, 7), v.Subtotal, esPar);
                DataCeldaMoneda(ws.Cell(fila, 8), v.Impuestos, esPar);
                DataCeldaMoneda(ws.Cell(fila, 9), v.Total, esPar);
                DataCelda(ws.Cell(fila, 10), v.NumeroAutorizacion ?? "-", esPar);
                DataCelda(ws.Cell(fila, 11), v.Notas ?? "", esPar);
            }

            // Fila de totales
            int totalFila = lista.Count + 2;
            var labelTotal = ws.Range(totalFila, 1, totalFila, 6).Merge();
            labelTotal.Value = "TOTALES";
            EstiloTotal(labelTotal.Style);

            var cSub   = ws.Cell(totalFila, 7);
            var cIva   = ws.Cell(totalFila, 8);
            var cTotal = ws.Cell(totalFila, 9);

            cSub.FormulaA1   = $"=SUM(G2:G{totalFila - 1})";
            cIva.FormulaA1   = $"=SUM(H2:H{totalFila - 1})";
            cTotal.FormulaA1 = $"=SUM(I2:I{totalFila - 1})";

            foreach (var c in new[] { cSub, cIva, cTotal })
            {
                EstiloTotal(c.Style);
                c.Style.NumberFormat.Format = "$#,##0.00";
            }

            // Auto-filtro en encabezado
            ws.RangeUsed()?.SetAutoFilter();
        }

        // ── PDF ───────────────────────────────────────────────────────────────────
        public string GenerarPdf(IEnumerable<Voucher> vouchers, string? nombreArchivo = null)
        {
#if ANDROID
            return GenerarPdfVouchersSkia(vouchers, nombreArchivo);
#else
            var lista = vouchers.OrderByDescending(v => v.Fecha).ToList();
            if (!lista.Any()) throw new InvalidOperationException("No hay vouchers para exportar.");

            var ruta = RutaTemporal(nombreArchivo ?? $"vouchers_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(9));

                    page.Header().Element(c => Encabezado(c, lista));
                    page.Content().PaddingTop(8).Column(col =>
                    {
                        col.Item().Element(c => SeccionResumen(c, lista));
                        col.Item().PaddingTop(12).Element(c => SeccionDetalle(c, lista));
                    });
                    page.Footer().AlignRight().Text(t =>
                    {
                        t.Span($"Total de vouchers: {lista.Count}  •  " +
                               $"Total general: {lista.Sum(v => v.Total):C2}  •  Página ");
                        t.CurrentPageNumber();
                        t.Span(" de ");
                        t.TotalPages();
                    });
                });
            }).GeneratePdf(ruta);

            return ruta;
#endif
        }

        #if !ANDROID
        private void Encabezado(IContainer c, List<Voucher> lista)
        {
            c.Background(COLOR_MORADO).Padding(12).Column(col =>
            {
                col.Item().AlignCenter()
                   .Text("REPORTE DE VOUCHERS")
                   .FontColor(PdfColors.White).FontSize(16).Bold();

                col.Item().AlignCenter()
                   .Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}")
                   .FontColor(PdfColors.White).FontSize(9).Italic();
            });
        }

        private void SeccionResumen(IContainer c, List<Voucher> lista)
        {
            c.Column(col =>
            {
                col.Item().PaddingTop(4).PaddingBottom(4)
                   .Text("Resumen por tipo de pago").FontSize(11).Bold()
                   .FontColor(COLOR_MORADO);

                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        cd.RelativeColumn(3);
                        cd.RelativeColumn(1.5f);
                        cd.RelativeColumn(2);
                    });

                    // Encabezados
                    t.Header(header =>
                    {
                        foreach (var txt in new[] { "Tipo de Pago", "Cantidad", "Total" })
                            header.Cell().Background(COLOR_MORADO).Padding(5)
                             .AlignCenter().Text(txt).FontColor(PdfColors.White).Bold().FontSize(9);
                    });

                    var grupos = lista.GroupBy(v => v.TipoPago).OrderBy(g => g.Key.ToString());
                    bool par = false;

                    foreach (var g in grupos)
                    {
                        string bg = par ? COLOR_MORADO_LITE : PdfColors.White;
                        t.Cell().Background(bg).Padding(4).Text(g.Key.ToString());
                        t.Cell().Background(bg).Padding(4).AlignCenter().Text(g.Count().ToString());
                        t.Cell().Background(bg).Padding(4).AlignRight()
                         .Text($"{g.Sum(v => v.Total):C2}");
                        par = !par;
                    }

                    // Total general
                    t.Cell().Background(COLOR_MORADO_MED).Padding(5).Text("TOTAL GENERAL").Bold();
                    t.Cell().Background(COLOR_MORADO_MED).Padding(5).AlignCenter()
                     .Text(lista.Count.ToString()).Bold();
                    t.Cell().Background(COLOR_MORADO_MED).Padding(5).AlignRight()
                     .Text($"{lista.Sum(v => v.Total):C2}").Bold();
                });
            });
        }

        private void SeccionDetalle(IContainer c, List<Voucher> lista)
        {
            c.Column(col =>
            {
                col.Item().PaddingBottom(4)
                   .Text("Detalle de vouchers").FontSize(11).Bold()
                   .FontColor(COLOR_MORADO);

                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        cd.ConstantColumn(22);   // #
                        cd.RelativeColumn(2.2f); // Fecha
                        cd.RelativeColumn(3.5f); // Comercio
                        cd.RelativeColumn(1.5f); // Tipo
                        cd.RelativeColumn(1.3f); // Tarjeta
                        cd.RelativeColumn(1.8f); // Total
                        cd.RelativeColumn(1.8f); // Autorización
                    });

                    // Encabezados
                    t.Header(header =>
                    {
                        foreach (var txt in new[] { "#","Fecha","Comercio","Tipo","Tarjeta","Total","Autorización" })
                            header.Cell().Background(COLOR_MORADO).Padding(4)
                             .AlignCenter().Text(txt).FontColor(PdfColors.White).Bold().FontSize(8);
                    });

                    bool par = false;
                    for (int i = 0; i < lista.Count; i++)
                    {
                        var v  = lista[i];
                        string bg = par ? "#F5F5F5" : PdfColors.White;

                        t.Cell().Background(bg).Padding(3).AlignCenter().Text((i + 1).ToString()).FontSize(8);
                        t.Cell().Background(bg).Padding(3).Text(v.Fecha.ToString("dd/MM/yy HH:mm")).FontSize(8);
                        t.Cell().Background(bg).Padding(3).Text(v.Comercio ?? "-").FontSize(8);
                        t.Cell().Background(bg).Padding(3).AlignCenter().Text(v.TipoPago.ToString()).FontSize(8);
                        t.Cell().Background(bg).Padding(3).AlignCenter()
                         .Text(string.IsNullOrEmpty(v.UltimosDigitosTarjeta) ? "-" : $"****{v.UltimosDigitosTarjeta}").FontSize(8);
                        t.Cell().Background(bg).Padding(3).AlignRight()
                         .Text($"{v.Total:C2}").FontSize(8).Bold();
                        t.Cell().Background(bg).Padding(3).AlignCenter()
                         .Text(v.NumeroAutorizacion ?? "-").FontSize(8);

                        par = !par;
                    }

                    // Fila de totales
                    for (int col2 = 0; col2 < 5; col2++)
                        t.Cell().Background(COLOR_MORADO_MED).Padding(4).Text("").FontSize(8);

                    t.Cell().Background(COLOR_MORADO_MED).Padding(4).AlignRight()
                     .Text($"{lista.Sum(v => v.Total):C2}").Bold().FontSize(9);
                                     t.Cell().Background(COLOR_MORADO_MED).Padding(4).Text("").FontSize(8);
                                    });
                                });
                            }
                    #endif

                            // ── Helpers de estilo Excel

        private void EstiloTitulo(IXLStyle s)
        {
            s.Font.Bold      = true;
            s.Font.FontSize  = 14;
            s.Font.FontColor = XLColor.White;
            s.Fill.BackgroundColor = XLColor.FromHtml(COLOR_MORADO);
            s.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            s.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
        }

        private void EstiloTotal(IXLStyle s)
        {
            s.Font.Bold      = true;
            s.Font.FontSize  = 10;
            s.Fill.BackgroundColor = XLColor.FromHtml(COLOR_MORADO_MED);
            s.Border.OutsideBorder      = XLBorderStyleValues.Thin;
            s.Border.OutsideBorderColor = XLColor.FromHtml("BDBDBD");
        }

        private void EncabezadoCelda(IXLCell c, string valor)
        {
            c.Value = valor;
            c.Style.Font.Bold      = true;
            c.Style.Font.FontSize  = 10;
            c.Style.Font.FontColor = XLColor.White;
            c.Style.Fill.BackgroundColor = XLColor.FromHtml(COLOR_MORADO);
            c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            c.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            c.Style.Alignment.WrapText   = true;
            c.Style.Border.OutsideBorder      = XLBorderStyleValues.Thin;
            c.Style.Border.OutsideBorderColor = XLColor.FromHtml("BDBDBD");
        }

        private void DataCelda(IXLCell c, object valor, bool esPar,
            XLAlignmentHorizontalValues alineacion = XLAlignmentHorizontalValues.Left)
        {
            c.Value           = XLCellValue.FromObject(valor);
            c.Style.Font.FontSize  = 10;
            c.Style.Fill.BackgroundColor = XLColor.FromHtml(esPar ? COLOR_FILA_PAR : "FFFFFF");
            c.Style.Alignment.Horizontal = alineacion;
            c.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            c.Style.Border.OutsideBorder      = XLBorderStyleValues.Thin;
            c.Style.Border.OutsideBorderColor = XLColor.FromHtml("E0E0E0");
        }

        private void DataCeldaMoneda(IXLCell c, decimal valor, bool esPar)
        {
            DataCelda(c, valor, esPar, XLAlignmentHorizontalValues.Right);
            c.Style.NumberFormat.Format = "$#,##0.00";
        }

        // ── Utilidades ────────────────────────────────────────────────────────────

        private string RutaTemporal(string nombreArchivo)
        {
            var dir = Path.Combine(FileSystem.CacheDirectory, "exports");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, nombreArchivo);
        }

        public async Task CompartirArchivoAsync(string rutaArchivo, string titulo)
        {
            if (!File.Exists(rutaArchivo))
                throw new FileNotFoundException("No se encontró el archivo para compartir.", rutaArchivo);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = titulo,
                File  = new ShareFile(rutaArchivo)
            });
        }

        // ── Movimientos Excel/PDF ─────────────────────────────────────────────────

        public string GenerarExcelMovimientos(IEnumerable<Models.MovimientoEfectivo> movimientos, string? nombreArchivo = null)
        {
            var lista = movimientos.OrderByDescending(m => m.Fecha).ToList();
            if (!lista.Any()) throw new InvalidOperationException("No hay movimientos para exportar.");

            var ruta = RutaTemporal(nombreArchivo ?? $"movimientos_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Movimientos");
            ws.ShowGridLines = false;

            // Título
            var tit = ws.Range("A1:F1").Merge();
            tit.Value = "REPORTE DE MOVIMIENTOS";
            EstiloTitulo(tit.Style);
            ws.Row(1).Height = 28;

            var sub = ws.Range("A2:F2").Merge();
            sub.Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}";
            sub.Style.Font.Italic = true;
            sub.Style.Font.FontColor = XLColor.Gray;
            sub.Style.Font.FontSize = 9;
            sub.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            string[] encabezados = { "Fecha", "Tipo", "Concepto", "Descripción", "Monto", "Responsable" };
            double[]  anchos     = { 18, 12, 24, 28, 14, 18 };
            for (int i = 0; i < encabezados.Length; i++)
            {
                EncabezadoCelda(ws.Cell(3, i + 1), encabezados[i]);
                ws.Column(i + 1).Width = anchos[i];
            }
            ws.Row(3).Height = 20;

            decimal totalEntradas = 0, totalSalidas = 0;
            for (int i = 0; i < lista.Count; i++)
            {
                var m = lista[i];
                int fila = i + 4;
                bool esPar = fila % 2 == 0;
                DataCelda(ws.Cell(fila, 1), m.Fecha.ToString("dd/MM/yyyy"), esPar);
                DataCelda(ws.Cell(fila, 2), m.Tipo == Models.TipoMovimiento.Entrada ? "Entrada" : "Salida", esPar,
                    XLAlignmentHorizontalValues.Center);
                ws.Cell(fila, 2).Style.Font.FontColor = m.Tipo == Models.TipoMovimiento.Entrada
                    ? XLColor.FromHtml("2E7D32") : XLColor.FromHtml("C62828");
                DataCelda(ws.Cell(fila, 3), m.Concepto, esPar);
                DataCelda(ws.Cell(fila, 4), m.Descripcion ?? "", esPar);
                DataCeldaMoneda(ws.Cell(fila, 5), m.Tipo == Models.TipoMovimiento.Entrada ? m.Monto : -m.Monto, esPar);
                ws.Cell(fila, 5).Style.Font.FontColor = m.Tipo == Models.TipoMovimiento.Entrada
                    ? XLColor.FromHtml("2E7D32") : XLColor.FromHtml("C62828");
                DataCelda(ws.Cell(fila, 6), m.Responsable ?? "", esPar);

                if (m.Tipo == Models.TipoMovimiento.Entrada) totalEntradas += m.Monto;
                else totalSalidas += m.Monto;
            }

            int filaTotal = lista.Count + 4;
            var lblTotal = ws.Range(filaTotal, 1, filaTotal, 4).Merge();
            lblTotal.Value = "RESUMEN";
            EstiloTotal(lblTotal.Style);

            var cEnt = ws.Cell(filaTotal, 5);
            cEnt.Value = totalEntradas;
            EstiloTotal(cEnt.Style);
            cEnt.Style.NumberFormat.Format = "$#,##0.00";
            cEnt.Style.Font.FontColor = XLColor.FromHtml("2E7D32");

            int filaTotal2 = filaTotal + 1;
            var lblSal = ws.Range(filaTotal2, 1, filaTotal2, 4).Merge();
            lblSal.Value = "Entradas / Salidas / Saldo";
            EstiloTotal(lblSal.Style);
            ws.Cell(filaTotal2, 5).Value = $"+${totalEntradas:F2}  /  -${totalSalidas:F2}  =  ${totalEntradas - totalSalidas:F2}";
            EstiloTotal(ws.Cell(filaTotal2, 5).Style);

            ws.RangeUsed()?.SetAutoFilter();
            wb.SaveAs(ruta);
            return ruta;
        }

        public string GenerarPdfMovimientos(IEnumerable<Models.MovimientoEfectivo> movimientos, string? nombreArchivo = null)
        {
#if ANDROID
            return GenerarPdfMovimientosSkia(movimientos, nombreArchivo);
#else
            var lista = movimientos.OrderByDescending(m => m.Fecha).ToList();
            if (!lista.Any()) throw new InvalidOperationException("No hay movimientos para exportar.");

            var ruta = RutaTemporal(nombreArchivo ?? $"movimientos_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(9));

                    page.Header().Background(COLOR_MORADO).Padding(10).Column(col =>
                    {
                        col.Item().AlignCenter()
                           .Text("REPORTE DE MOVIMIENTOS")
                           .FontColor(PdfColors.White).FontSize(16).Bold();
                        col.Item().AlignCenter()
                           .Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}")
                           .FontColor(PdfColors.White).FontSize(9).Italic();
                    });

                    page.Content().PaddingTop(10).Column(col =>
                    {
                        decimal entradas = lista.Where(m => m.Tipo == Models.TipoMovimiento.Entrada).Sum(m => m.Monto);
                        decimal salidas  = lista.Where(m => m.Tipo == Models.TipoMovimiento.Salida).Sum(m => m.Monto);

                        col.Item().PaddingBottom(6).Row(row =>
                        {
                            row.RelativeItem().Background(COLOR_MORADO_LITE).Padding(6).Column(c2 =>
                            {
                                c2.Item().Text("Entradas").Bold().FontColor(COLOR_MORADO);
                                c2.Item().Text($"+${entradas:F2}").FontColor("2E7D32").Bold();
                            });
                            row.ConstantItem(8);
                            row.RelativeItem().Background(COLOR_MORADO_LITE).Padding(6).Column(c2 =>
                            {
                                c2.Item().Text("Salidas").Bold().FontColor(COLOR_MORADO);
                                c2.Item().Text($"-${salidas:F2}").FontColor("C62828").Bold();
                            });
                            row.ConstantItem(8);
                            row.RelativeItem().Background(COLOR_MORADO_MED).Padding(6).Column(c2 =>
                            {
                                c2.Item().Text("Saldo").Bold().FontColor(COLOR_MORADO);
                                c2.Item().Text($"${entradas - salidas:F2}").Bold();
                            });
                        });

                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            {
                                cd.RelativeColumn(1.8f); // Fecha
                                cd.RelativeColumn(1.2f); // Tipo
                                cd.RelativeColumn(2.5f); // Concepto
                                cd.RelativeColumn(2.5f); // Descripción
                                cd.RelativeColumn(1.8f); // Monto
                            });

                            t.Header(header =>
                            {
                                foreach (var txt in new[] { "Fecha", "Tipo", "Concepto", "Descripción", "Monto" })
                                    header.Cell().Background(COLOR_MORADO).Padding(5)
                                         .AlignCenter().Text(txt).FontColor(PdfColors.White).Bold().FontSize(9);
                            });

                            bool par = false;
                            foreach (var m in lista)
                            {
                                string bg = par ? COLOR_MORADO_LITE : PdfColors.White;
                                string colorTipo = m.Tipo == Models.TipoMovimiento.Entrada ? "2E7D32" : "C62828";
                                t.Cell().Background(bg).Padding(3).Text(m.Fecha.ToString("dd/MM/yyyy")).FontSize(8);
                                t.Cell().Background(bg).Padding(3).AlignCenter()
                                 .Text(m.Tipo == Models.TipoMovimiento.Entrada ? "Entrada" : "Salida")
                                 .FontColor(colorTipo).Bold().FontSize(8);
                                t.Cell().Background(bg).Padding(3).Text(m.Concepto ?? "").FontSize(8);
                                t.Cell().Background(bg).Padding(3).Text(m.Descripcion ?? "").FontSize(8);
                                t.Cell().Background(bg).Padding(3).AlignRight()
                                 .Text($"{(m.Tipo == Models.TipoMovimiento.Entrada ? "+" : "-")}${m.Monto:F2}")
                                 .FontColor(colorTipo).Bold().FontSize(8);
                                par = !par;
                            }
                        });
                    });

                    page.Footer().AlignRight().Text(t =>
                    {
                        t.Span($"Total: {lista.Count} movimientos  •  Página ");
                        t.CurrentPageNumber();
                        t.Span(" de ");
                        t.TotalPages();
                    });
                });
            }).GeneratePdf(ruta);

            return ruta;
#endif
        }

#if ANDROID
#pragma warning disable CS0618
        private string GenerarPdfMovimientosSkia(IEnumerable<Models.MovimientoEfectivo> movimientos, string? nombreArchivo = null)
        {
            var lista = movimientos.OrderByDescending(m => m.Fecha).ToList();
            if (!lista.Any()) throw new InvalidOperationException("No hay movimientos para exportar.");

            var ruta = RutaTemporal(nombreArchivo ?? $"movimientos_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

            const float pageW = 595f;   // A4 ancho en puntos
            const float pageH = 842f;   // A4 alto en puntos
            const float margin = 42f;
            const float contentW = pageW - margin * 2;
            const float rowH = 18f;
            const float headerH = 50f;
            const float summaryH = 60f;

            // Anchos de columna (proporcionales a contentW)
            float[] colW = { contentW * 0.18f, contentW * 0.12f, contentW * 0.25f, contentW * 0.25f, contentW * 0.20f };
            string[] colTitles = { "Fecha", "Tipo", "Concepto", "Descripción", "Monto" };

            using var stream = new FileStream(ruta, FileMode.Create, FileAccess.Write);
            using var doc = SKDocument.CreatePdf(stream);

            decimal entradas = lista.Where(m => m.Tipo == Models.TipoMovimiento.Entrada).Sum(m => m.Monto);
            decimal salidas  = lista.Where(m => m.Tipo == Models.TipoMovimiento.Salida).Sum(m => m.Monto);
            decimal saldo    = entradas - salidas;

            int itemsPerPage = (int)((pageH - margin * 2 - headerH - summaryH - rowH - 30f) / rowH);
            int totalPages   = (int)Math.Ceiling(lista.Count / (double)Math.Max(1, itemsPerPage));
            if (totalPages == 0) totalPages = 1;

            using var paintBg       = new SKPaint { Color = SKColor.Parse("673AB7"), Style = SKPaintStyle.Fill };
            using var paintSummaryBg = new SKPaint { Color = SKColor.Parse("EDE7F6"), Style = SKPaintStyle.Fill };
            using var paintHeaderRow = new SKPaint { Color = SKColor.Parse("673AB7"), Style = SKPaintStyle.Fill };
            using var paintRowEven  = new SKPaint { Color = SKColor.Parse("EDE7F6"), Style = SKPaintStyle.Fill };
            using var paintRowOdd   = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
            using var paintWhiteTxt = new SKPaint { Color = SKColors.White, TextSize = 14f, IsAntialias = true };
            using var paintWhiteSm  = new SKPaint { Color = SKColors.White, TextSize = 9f,  IsAntialias = true };
            using var paintPurpleTxt = new SKPaint { Color = SKColor.Parse("673AB7"), TextSize = 9f, IsAntialias = true, FakeBoldText = true };
            using var paintBlackSm  = new SKPaint { Color = SKColors.Black, TextSize = 8f, IsAntialias = true };
            using var paintGreenSm  = new SKPaint { Color = SKColor.Parse("2E7D32"), TextSize = 8f, IsAntialias = true, FakeBoldText = true };
            using var paintRedSm    = new SKPaint { Color = SKColor.Parse("C62828"), TextSize = 8f, IsAntialias = true, FakeBoldText = true };
            using var paintHeaderTxt = new SKPaint { Color = SKColors.White, TextSize = 8f, IsAntialias = true, FakeBoldText = true };
            using var paintGrayTxt  = new SKPaint { Color = SKColors.Gray, TextSize = 8f, IsAntialias = true };

            for (int page = 0; page < totalPages; page++)
            {
                var canvas = doc.BeginPage(pageW, pageH);

                // ── Header ──
                canvas.DrawRect(0, 0, pageW, headerH, paintBg);
                canvas.DrawText("REPORTE DE MOVIMIENTOS", margin, 22f, paintWhiteTxt);
                canvas.DrawText($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}", margin, 38f, paintWhiteSm);

                float y = headerH + 10f;

                // ── Resumen (solo primera página) ──
                if (page == 0)
                {
                    float sumW = (contentW - 16f) / 3f;
                    float[] sumX = { margin, margin + sumW + 8f, margin + (sumW + 8f) * 2 };
                    string[] sumLabels = { "Entradas", "Salidas", "Saldo" };
                    string[] sumVals   = { $"+${entradas:F2}", $"-${salidas:F2}", $"${saldo:F2}" };
                    SKPaint[] sumValPaints = { paintGreenSm, paintRedSm, paintPurpleTxt };

                    for (int i = 0; i < 3; i++)
                    {
                        canvas.DrawRect(sumX[i], y, sumW, summaryH - 10f, paintSummaryBg);
                        canvas.DrawText(sumLabels[i], sumX[i] + 6f, y + 16f, paintPurpleTxt);
                        canvas.DrawText(sumVals[i],   sumX[i] + 6f, y + 32f, sumValPaints[i]);
                    }
                    y += summaryH;
                }

                // ── Encabezado tabla ──
                float x = margin;
                canvas.DrawRect(x, y, contentW, rowH, paintHeaderRow);
                for (int c = 0; c < colTitles.Length; c++)
                {
                    canvas.DrawText(colTitles[c], x + 3f, y + rowH - 5f, paintHeaderTxt);
                    x += colW[c];
                }
                y += rowH;

                // ── Filas ──
                int startIdx = page * itemsPerPage;
                int endIdx   = Math.Min(startIdx + itemsPerPage, lista.Count);
                for (int i = startIdx; i < endIdx; i++)
                {
                    var m   = lista[i];
                    bool par = i % 2 == 0;
                    canvas.DrawRect(margin, y, contentW, rowH, par ? paintRowEven : paintRowOdd);

                    x = margin;
                    string signo = m.Tipo == Models.TipoMovimiento.Entrada ? "+" : "-";
                    var valPaint = m.Tipo == Models.TipoMovimiento.Entrada ? paintGreenSm : paintRedSm;

                    canvas.DrawText(m.Fecha.ToString("dd/MM/yyyy"), x + 3f, y + rowH - 5f, paintBlackSm); x += colW[0];
                    canvas.DrawText(m.Tipo == Models.TipoMovimiento.Entrada ? "Entrada" : "Salida", x + 3f, y + rowH - 5f, valPaint); x += colW[1];
                    canvas.DrawText(Truncar(m.Concepto ?? "", 28), x + 3f, y + rowH - 5f, paintBlackSm); x += colW[2];
                    canvas.DrawText(Truncar(m.Descripcion ?? "", 28), x + 3f, y + rowH - 5f, paintBlackSm); x += colW[3];
                    canvas.DrawText($"{signo}${m.Monto:F2}", x + 3f, y + rowH - 5f, valPaint);

                    y += rowH;
                }

                // ── Footer ──
                float footerY = pageH - margin + 5f;
                canvas.DrawText($"Total: {lista.Count} movimientos  •  Página {page + 1} de {totalPages}", margin, footerY, paintGrayTxt);

                doc.EndPage();
            }

            doc.Close();
            return ruta;
        }

        private static string Truncar(string texto, int max) =>
            texto.Length <= max ? texto : texto[..max] + "…";

        private string GenerarPdfVouchersSkia(IEnumerable<Voucher> vouchers, string? nombreArchivo = null)
        {
            var lista = vouchers.OrderByDescending(v => v.Fecha).ToList();
            if (!lista.Any()) throw new InvalidOperationException("No hay vouchers para exportar.");

            var ruta = RutaTemporal(nombreArchivo ?? $"vouchers_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

            const float pageW    = 595f;
            const float pageH    = 842f;
            const float margin   = 42f;
            const float contentW = pageW - margin * 2;
            const float rowH     = 18f;
            const float headerH  = 50f;
            const float summaryH = 60f;

            float[] colW      = { contentW * 0.18f, contentW * 0.22f, contentW * 0.15f, contentW * 0.13f, contentW * 0.17f, contentW * 0.15f };
            string[] colTitles = { "Fecha", "Comercio", "Tipo Pago", "Tarjeta", "Total", "Autorización" };

            using var stream = new FileStream(ruta, FileMode.Create, FileAccess.Write);
            using var doc    = SKDocument.CreatePdf(stream);

            int itemsPerPage = (int)((pageH - margin * 2 - headerH - summaryH - rowH - 30f) / rowH);
            int totalPages   = (int)Math.Ceiling(lista.Count / (double)Math.Max(1, itemsPerPage));
            if (totalPages == 0) totalPages = 1;

            using var paintBg        = new SKPaint { Color = SKColor.Parse("673AB7"), Style = SKPaintStyle.Fill };
            using var paintSummaryBg = new SKPaint { Color = SKColor.Parse("EDE7F6"), Style = SKPaintStyle.Fill };
            using var paintHeaderRow = new SKPaint { Color = SKColor.Parse("673AB7"), Style = SKPaintStyle.Fill };
            using var paintRowEven   = new SKPaint { Color = SKColor.Parse("EDE7F6"), Style = SKPaintStyle.Fill };
            using var paintRowOdd    = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
            using var paintTotalRow  = new SKPaint { Color = SKColor.Parse("D1C4E9"), Style = SKPaintStyle.Fill };
            using var paintWhiteTxt  = new SKPaint { Color = SKColors.White, TextSize = 14f, IsAntialias = true };
            using var paintWhiteSm   = new SKPaint { Color = SKColors.White, TextSize = 9f,  IsAntialias = true };
            using var paintPurpleTxt = new SKPaint { Color = SKColor.Parse("673AB7"), TextSize = 9f, IsAntialias = true, FakeBoldText = true };
            using var paintBlackSm   = new SKPaint { Color = SKColors.Black, TextSize = 8f, IsAntialias = true };
            using var paintBoldSm    = new SKPaint { Color = SKColors.Black, TextSize = 8f, IsAntialias = true, FakeBoldText = true };
            using var paintHeaderTxt = new SKPaint { Color = SKColors.White, TextSize = 8f, IsAntialias = true, FakeBoldText = true };
            using var paintGrayTxt   = new SKPaint { Color = SKColors.Gray, TextSize = 8f, IsAntialias = true };

            var culture = CultureInfo.CurrentCulture;

            for (int page = 0; page < totalPages; page++)
            {
                var canvas = doc.BeginPage(pageW, pageH);

                // ── Header ──
                canvas.DrawRect(0, 0, pageW, headerH, paintBg);
                canvas.DrawText("REPORTE DE VOUCHERS", margin, 22f, paintWhiteTxt);
                canvas.DrawText($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}", margin, 38f, paintWhiteSm);

                float y = headerH + 10f;

                // ── Resumen (solo primera página) ──
                if (page == 0)
                {
                    var grupos   = lista.GroupBy(v => v.TipoPago).OrderBy(g => g.Key.ToString()).ToList();
                    decimal total = lista.Sum(v => v.Total);
                    float sumW   = (contentW - (grupos.Count - 1) * 8f) / Math.Max(1, grupos.Count);

                    for (int i = 0; i < grupos.Count; i++)
                    {
                        float sx = margin + i * (sumW + 8f);
                        canvas.DrawRect(sx, y, sumW, summaryH - 10f, paintSummaryBg);
                        canvas.DrawText(grupos[i].Key.ToString(), sx + 6f, y + 16f, paintPurpleTxt);
                        canvas.DrawText($"{grupos[i].Count()} voucher(s)", sx + 6f, y + 30f, paintBlackSm);
                        canvas.DrawText($"{grupos[i].Sum(v => v.Total):C2}", sx + 6f, y + 44f, paintBoldSm);
                    }
                    y += summaryH;
                }

                // ── Encabezado tabla ──
                float x = margin;
                canvas.DrawRect(x, y, contentW, rowH, paintHeaderRow);
                for (int c = 0; c < colTitles.Length; c++)
                {
                    canvas.DrawText(colTitles[c], x + 3f, y + rowH - 5f, paintHeaderTxt);
                    x += colW[c];
                }
                y += rowH;

                // ── Filas ──
                int startIdx = page * itemsPerPage;
                int endIdx   = Math.Min(startIdx + itemsPerPage, lista.Count);
                for (int i = startIdx; i < endIdx; i++)
                {
                    var v   = lista[i];
                    bool par = i % 2 == 0;
                    canvas.DrawRect(margin, y, contentW, rowH, par ? paintRowEven : paintRowOdd);

                    x = margin;
                    canvas.DrawText(v.Fecha.ToString("dd/MM/yy HH:mm"), x + 3f, y + rowH - 5f, paintBlackSm); x += colW[0];
                    canvas.DrawText(Truncar(v.Comercio ?? "-", 22),       x + 3f, y + rowH - 5f, paintBlackSm); x += colW[1];
                    canvas.DrawText(v.TipoPago.ToString(),                 x + 3f, y + rowH - 5f, paintBlackSm); x += colW[2];
                    canvas.DrawText(string.IsNullOrEmpty(v.UltimosDigitosTarjeta) ? "-" : $"****{v.UltimosDigitosTarjeta}",
                                    x + 3f, y + rowH - 5f, paintBlackSm); x += colW[3];
                    canvas.DrawText($"{v.Total:C2}",                       x + 3f, y + rowH - 5f, paintBoldSm);  x += colW[4];
                    canvas.DrawText(v.NumeroAutorizacion ?? "-",            x + 3f, y + rowH - 5f, paintBlackSm);

                    y += rowH;
                }

                // ── Fila totales (última página) ──
                if (page == totalPages - 1)
                {
                    canvas.DrawRect(margin, y, contentW, rowH, paintTotalRow);
                    x = margin;
                    canvas.DrawText("TOTAL", x + 3f, y + rowH - 5f, paintBoldSm); x += colW[0] + colW[1] + colW[2] + colW[3];
                    canvas.DrawText($"{lista.Sum(v => v.Total):C2}", x + 3f, y + rowH - 5f, paintBoldSm);
                    y += rowH;
                }

                // ── Footer ──
                float footerY = pageH - margin + 5f;
                canvas.DrawText($"Total: {lista.Count} vouchers  •  Página {page + 1} de {totalPages}", margin, footerY, paintGrayTxt);

                doc.EndPage();
            }

            doc.Close();
            return ruta;
        }
#pragma warning restore CS0618
#endif
    }
}
