using CajaApp.ViewModels;
using CajaApp.Models;
using CajaApp.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Globalization;
#if ANDROID
using Android.OS;
#endif

namespace CajaApp.Views
{
    public partial class VoucherScannerPage : ContentPage
    {
        private readonly VoucherViewModel _viewModel;
        private bool _todosSeleccionados = false;

        public VoucherScannerPage(VoucherViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            // Solo recarga si la lista está vacía para evitar consultas innecesarias a la DB
            if (!_viewModel.Vouchers.Any())
                await _viewModel.CargarVouchers();
            ActualizarItemsPicker();
        }

        private void ActualizarItemsPicker()
        {
            TipoPagoPicker.Items.Clear();
            TipoPagoPicker.Items.Add(L["Voucher_TipoCreditoItem"]);
            TipoPagoPicker.Items.Add(L["Voucher_TipoDebitoItem"]);
            TipoPagoPicker.Items.Add(L["Voucher_TipoEfectivoItem"]);
            TipoPagoPicker.Items.Add(L["Voucher_TipoTransferenciaItem"]);
            TipoPagoPicker.Items.Add(L["Voucher_TipoOtroItem"]);
        }

        // Alias cómodo en el code-behind:
        private static LocalizationService L => LocalizationService.Instance;

        // ── Escanear ────────────────────────────────────────────────────────────

        private async void OnEscanearCamaraClicked(object sender, EventArgs e)
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.Camera>();

                if (status != PermissionStatus.Granted)
                {
                    if (Permissions.ShouldShowRationale<Permissions.Camera>())
                        await DisplayAlert(
                            L["Voucher_PermisoCamara"],
                            L["Voucher_PermisoCamaraMsg"],
                            L["Btn_Aceptar"]);

                    status = await Permissions.RequestAsync<Permissions.Camera>();
                }

                if (status != PermissionStatus.Granted)
                {
                    bool open = await DisplayAlert(
                        L["Voucher_PermisoDenegado"],
                        L["Voucher_PermisoDenegadoMsg"],
                        L["Btn_AbrirAjustes"], L["Btn_Cancelar"]);
                    if (open) AppInfo.ShowSettingsUI();
                    return;
                }

                var photo = await MediaPicker.Default.CapturePhotoAsync();
                if (photo != null) await ProcesarImagen(photo);
            }
            catch (FeatureNotSupportedException)
            {
                await DisplayAlert(
                    L["Lbl_NoDisponible"],
                    L["Voucher_CamaraNoDisponible"],
                    L["Btn_Aceptar"]);
            }
            catch (Exception ex)
            {
                await DisplayAlert(
                    L["Lbl_Error"],
                    LocalizationService.GetF("Voucher_ErrorCamara", ex.Message),
                    L["Btn_Aceptar"]);
            }
        }

        private async void OnSeleccionarImagenClicked(object sender, EventArgs e)
        {
            try
            {
                var photo = await MediaPicker.Default.PickPhotoAsync();
                if (photo != null) await ProcesarImagen(photo);
            }
            catch (Exception ex)
            {
                await DisplayAlert(
                    L["Lbl_Error"],
                    LocalizationService.GetF("Voucher_ErrorSeleccionarImagen", ex.Message),
                    L["Btn_Aceptar"]);
            }
        }

        private async Task ProcesarImagen(FileResult photo)
        {
            try
            {
                using var stream = await photo.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);

                // Redimensionar antes de enviar al OCR: reduce el payload de red
                // de ~10 MB (foto full res) a <300 KB, acelerando la llamada a la API.
                var imagenService = new ImagenService();
                var imagenBytes = imagenService.RedimensionarBytes(ms.ToArray(), maxWidth: 1600, maxHeight: 1200);

                var voucher = await _viewModel.ProcesarImagenVoucherAsync(imagenBytes);
                voucher.RutaImagen = photo.FullPath;
                TipoPagoPicker.SelectedIndex = TipoPagoAIndice(voucher.TipoPago);
            }
            catch (Exception ex)
            {
                await DisplayAlert(
                    L["Lbl_Error"],
                    LocalizationService.GetF("Voucher_ErrorOCR", ex.Message),
                    L["Btn_Aceptar"]);
            }
        }

        // ── Guardar / Cancelar previsualización ─────────────────────────────────

        private async void OnGuardarVoucherClicked(object sender, EventArgs e)
        {
            var voucher = _viewModel.VoucherPrevisualizacion;
            if (voucher == null) return;

            try
            {
                voucher.Comercio = ComercioEntry.Text?.Trim() ?? string.Empty;
                voucher.TipoPago = IndiceTipoPago(TipoPagoPicker.SelectedIndex);

                if (decimal.TryParse(SubtotalEntry.Text, NumberStyles.Any,
                        CultureInfo.CurrentCulture, out var sub)) voucher.Subtotal = sub;
                if (decimal.TryParse(DescuentoEntry.Text, NumberStyles.Any,
                        CultureInfo.CurrentCulture, out var desc)) voucher.Descuentos = desc;
                if (decimal.TryParse(TotalEntry.Text, NumberStyles.Any,
                        CultureInfo.CurrentCulture, out var tot)) voucher.Total = tot;

                if (voucher.Impuestos == 0 && voucher.Total > voucher.Subtotal)
                    voucher.Impuestos = voucher.Total - voucher.Subtotal;

                bool ok = await _viewModel.GuardarVoucherAsync(voucher);
                if (ok)
                {
                    await DisplayAlert(
                        L["Voucher_Guardado"],
                        L["Voucher_GuardadoMsg"],
                        L["Btn_Aceptar"]);
                    _viewModel.LimpiarPrevisualizacion();
                    TipoPagoPicker.SelectedIndex = 0;
                }
                else
                    await DisplayAlert(
                        L["Lbl_Error"],
                        L["Voucher_ErrorGuardar"],
                        L["Btn_Aceptar"]);
            }
            catch (Exception ex)
            {
                await DisplayAlert(
                    L["Lbl_Error"],
                    LocalizationService.GetF("Voucher_ErrorAlGuardar", ex.Message),
                    L["Btn_Aceptar"]);
            }
        }

        private void OnCancelarVoucherClicked(object sender, EventArgs e)
        {
            _viewModel.LimpiarPrevisualizacion();
            TipoPagoPicker.SelectedIndex = 0;
        }

        // ── Eliminar todos ───────────────────────────────────────────────────────

        private async void OnEliminarTodosClicked(object sender, EventArgs e)
        {
            if (!_viewModel.Vouchers.Any())
            {
                await DisplayAlert(
                    L["Lbl_SinDatos"],
                    L["Voucher_SinDatos"],
                    L["Btn_Aceptar"]);
                return;
            }

            bool confirm = await DisplayAlert(
                L["Voucher_ConfirmarEliminarTodosTitulo"],
                L["Voucher_ConfirmarEliminarTodos"],
                L["Btn_Eliminar"], L["Btn_Cancelar"]);

            if (!confirm) return;

            bool ok = await _viewModel.EliminarTodosVouchersAsync();
            await DisplayAlert(
                ok ? L["Voucher_Eliminado"] : L["Lbl_Error"],
                ok ? L["Voucher_EliminadosTodos"] : L["Voucher_ErrorEliminarTodos"],
                L["Btn_Aceptar"]);
        }

        // ── Lista: eliminar / ver detalle ───────────────────────────────────────

        private async void OnEliminarVoucherClicked(object sender, EventArgs e)
        {
            if (sender is Button { CommandParameter: Voucher voucher })
            {
                bool confirm = await DisplayAlert(
                    L["Voucher_ConfirmarEliminarTitulo"],
                    LocalizationService.GetF("Voucher_ConfirmarEliminar", voucher.Comercio, voucher.TipoPagoTexto, voucher.TotalTexto, voucher.FechaTexto),
                    L["Btn_Eliminar"], L["Btn_Cancelar"]);

                if (confirm)
                {
                    bool ok = await _viewModel.EliminarVoucherAsync(voucher);
                    await DisplayAlert(
                        ok ? L["Voucher_Eliminado"] : L["Lbl_Error"],
                        ok ? L["Voucher_EliminadoMsg"] : L["Voucher_ErrorEliminar"],
                        L["Btn_Aceptar"]);
                }
            }
        }

        private async void OnVerDetalleClicked(object sender, EventArgs e)
        {
            if (sender is not Button { CommandParameter: Voucher voucher }) return;

            var opciones = new List<string> { "📋 Ver detalle" };
            if (!string.IsNullOrWhiteSpace(voucher.TextoCompleto))
                opciones.Add("🔍 Ver texto OCR completo");

            string accion = await DisplayActionSheet(
                $"Voucher — {voucher.Comercio}", L["Btn_Cerrar"], null, [.. opciones]);

            if (accion == "📋 Ver detalle")
                await DisplayAlert($"Detalle — {voucher.Comercio}", GenerarDetalle(voucher), L["Btn_Aceptar"]);
            else if (accion == "🔍 Ver texto OCR completo")
                await Navigation.PushAsync(new OcrTextPage(voucher.TextoCompleto));
        }

        private static string GenerarDetalle(Voucher v)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"VOUCHER #{v.NumeroVoucher}");
            sb.AppendLine($"Fecha: {v.FechaTexto}");
            sb.AppendLine($"Comercio: {v.Comercio}");
            sb.AppendLine($"Tipo: {v.TipoPagoTexto}");
            if (!string.IsNullOrEmpty(v.UltimosDigitosTarjeta))
                sb.AppendLine($"Tarjeta: ****{v.UltimosDigitosTarjeta}");
            sb.AppendLine($"Subtotal: ${v.Subtotal:F2}");
            if (v.Impuestos > 0) sb.AppendLine($"IVA: ${v.Impuestos:F2}");
            if (v.Descuentos > 0) sb.AppendLine($"Descuentos: -${v.Descuentos:F2}");
            sb.AppendLine($"TOTAL: ${v.Total:F2}");
            if (!string.IsNullOrEmpty(v.NumeroAutorizacion))
                sb.AppendLine($"Autorización: {v.NumeroAutorizacion}");
            if (!string.IsNullOrEmpty(v.ReferenciaBanco))
                sb.AppendLine($"Referencia: {v.ReferenciaBanco}");
            if (!string.IsNullOrEmpty(v.Notas))
                sb.AppendLine($"Notas: {v.Notas}");
            return sb.ToString();
        }

        // ── Selección múltiple ──────────────────────────────────────────────────

        private void OnSeleccionarTodosClicked(object sender, EventArgs e)
        {
            _todosSeleccionados = !_todosSeleccionados;
            _viewModel.SeleccionarTodos(_todosSeleccionados);
            SeleccionarTodosButton.Text = _todosSeleccionados ? L["Voucher_BtnNinguno"] : L["Voucher_BtnTodos"];
        }

        // ── Filtros de fecha ─────────────────────────────────────────────────────

        private void ActualizarBotonesFilto(Button activo)
        {
            var todos = new[] { FiltroTodosBtn, FiltroHoyBtn, FiltroSemanaBtn, FiltroMesBtn, FiltroAnioBtn, FiltroFechaBtn };
            foreach (var b in todos)
                b.BackgroundColor = Color.FromArgb("#607D8B");
            activo.BackgroundColor = Color.FromArgb("#673AB7");
            FechaPersonalizadaPicker.IsVisible = activo == FiltroFechaBtn;
        }

        private void OnOrdenFechaVoucherClicked(object sender, EventArgs e)
        {
            ActualizarBotonesOrden(OrdenFechaVoucherBtn);
            _viewModel.OrdenActivo = OrdenVoucher.FechaVoucher;
        }

        private void OnOrdenFechaEscaneoClicked(object sender, EventArgs e)
        {
            ActualizarBotonesOrden(OrdenFechaEscaneoBtn);
            _viewModel.OrdenActivo = OrdenVoucher.FechaEscaneo;
        }

        private void ActualizarBotonesOrden(Button activo)
        {
            var todos = new[] { OrdenFechaVoucherBtn, OrdenFechaEscaneoBtn };
            foreach (var b in todos)
                b.BackgroundColor = Color.FromArgb("#607D8B");
            activo.BackgroundColor = Color.FromArgb("#673AB7");
        }

        private void OnFiltroTodosClicked(object sender, EventArgs e)
        {
            ActualizarBotonesFilto(FiltroTodosBtn);
            _viewModel.FiltroActivo = FiltroFechaVoucher.Todos;
        }

        private void OnFiltroHoyClicked(object sender, EventArgs e)
        {
            ActualizarBotonesFilto(FiltroHoyBtn);
            _viewModel.FiltroActivo = FiltroFechaVoucher.Hoy;
        }

        private void OnFiltroSemanaClicked(object sender, EventArgs e)
        {
            ActualizarBotonesFilto(FiltroSemanaBtn);
            _viewModel.FiltroActivo = FiltroFechaVoucher.EstaSemana;
        }

        private void OnFiltroMesClicked(object sender, EventArgs e)
        {
            ActualizarBotonesFilto(FiltroMesBtn);
            _viewModel.FiltroActivo = FiltroFechaVoucher.EsteMes;
        }

        private void OnFiltroAnioClicked(object sender, EventArgs e)
        {
            ActualizarBotonesFilto(FiltroAnioBtn);
            _viewModel.FiltroActivo = FiltroFechaVoucher.EsteAnio;
        }

        private void OnFiltroFechaClicked(object sender, EventArgs e)
        {
            ActualizarBotonesFilto(FiltroFechaBtn);
            FechaPersonalizadaPicker.Date = _viewModel.FechaPersonalizada;
            _viewModel.FiltroActivo = FiltroFechaVoucher.FechaPersonalizada;
        }

        private void OnFechaPersonalizadaChanged(object sender, DateChangedEventArgs e)
        {
            _viewModel.FechaPersonalizada = e.NewDate;
        }

        // ── Exportar ────────────────────────────────────────────────────────────

        private async void OnExportarClicked(object sender, EventArgs e)
        {
            if (!_viewModel.Vouchers.Any())
            {
                await DisplayAlert(
                    L["Lbl_SinDatos"],
                    L["Voucher_SinDatos"],
                    L["Btn_Aceptar"]);
                return;
            }

            int seleccionados = _viewModel.ContarSeleccionados();
            string alcance = seleccionados > 0
                ? $"{seleccionados} voucher(s) seleccionado(s)"
                : $"{_viewModel.Vouchers.Count} voucher(s) — {_viewModel.ObtenerDescripcionFiltro()}";

            string formato = await DisplayActionSheet(
                LocalizationService.GetF("Voucher_ExportarTitulo", alcance),
                L["Btn_Cancelar"],
                null,
                L["Voucher_FormatoExcel"],
                L["Voucher_FormatoPDF"],
                L["Voucher_FormatoAmbos"]);

            if (formato == null || formato == L["Btn_Cancelar"]) return;

            try
            {
                if (formato == L["Voucher_FormatoExcel"])
                    await _viewModel.ExportarExcelAsync();
                else if (formato == L["Voucher_FormatoPDF"])
                    await _viewModel.ExportarPdfAsync();
                else if (formato == L["Voucher_FormatoAmbos"])
                {
                    await _viewModel.ExportarExcelAsync();
                    await _viewModel.ExportarPdfAsync();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert(
                    L["Lbl_Error"],
                    LocalizationService.GetF("Voucher_ErrorExportar", ex.Message),
                    L["Btn_Aceptar"]);
            }
        }

        // ── Ver OCR ─────────────────────────────────────────────────────────────

        private async void OnVerTextoOCRClicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_viewModel.TextoOCR))
                await Navigation.PushAsync(new OcrTextPage(_viewModel.TextoOCR));
        }

        // ── Reporte de texto ────────────────────────────────────────────────────

        private async void OnGenerarReporteVouchersClicked(object sender, EventArgs e)
        {
            try
            {
                string reporte = _viewModel.GenerarReporteVouchers();
                string titulo  = $"{L["Voucher_ReporteTitulo"]} — {_viewModel.ObtenerDescripcionFiltro()}";
                string accion  = await DisplayActionSheet(
                    L["Voucher_ReporteAccion"],
                    L["Btn_Cancelar"], null,
                    L["Voucher_VerReporte"], L["Voucher_CompartirReporte"]);

                if (accion == L["Voucher_VerReporte"])
                    await DisplayAlert(titulo, reporte, L["Btn_Aceptar"]);
                else if (accion == L["Voucher_CompartirReporte"])
                    await Share.Default.RequestAsync(new ShareTextRequest
                        { Text = reporte, Title = titulo });
            }
            catch (Exception ex)
            {
                await DisplayAlert(
                    L["Lbl_Error"],
                    LocalizationService.GetF("Voucher_ErrorReporte", ex.Message),
                    L["Btn_Aceptar"]);
            }
        }

        // ── Mapeo TipoPago ↔ índice del Picker ──────────────────────────────────
        // Mapeo explícito para no depender de que los valores del enum sean
        // consecutivos desde 1 ni de que el orden del Picker no cambie.

        private static int TipoPagoAIndice(TipoPago tipo) => tipo switch
        {
            TipoPago.Credito       => 0,
            TipoPago.Debito        => 1,
            TipoPago.Efectivo      => 2,
            TipoPago.Transferencia => 3,
            TipoPago.Otro          => 4,
            _                      => 0
        };

        private static TipoPago IndiceTipoPago(int index) => index switch
        {
            0 => TipoPago.Credito,
            1 => TipoPago.Debito,
            2 => TipoPago.Efectivo,
            3 => TipoPago.Transferencia,
            4 => TipoPago.Otro,
            _ => TipoPago.Otro
        };
    }

    // ── Página de texto OCR copiable ─────────────────────────────────────────────

    public class OcrTextPage : ContentPage
    {
        public OcrTextPage(string textoOCR)
        {
            Title = "Texto OCR extraído";
            this.SetDynamicResource(BackgroundColorProperty, "BackgroundColor");

            var editor = new Editor
            {
                Text              = textoOCR,
                IsReadOnly        = true,
                FontFamily        = "Courier New",
                FontSize          = 13,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions   = LayoutOptions.Fill,
            };
            editor.SetDynamicResource(BackgroundColorProperty, "SurfaceColor");
            editor.SetDynamicResource(Editor.TextColorProperty, "TextColor");

            var btnCompartir = new Button
            {
                Text            = "📤 Compartir texto",
                BackgroundColor = Color.FromArgb("#2196F3"),
                TextColor       = Colors.White,
                CornerRadius    = 8,
                Margin          = new Thickness(10, 5, 10, 20),
            };
            btnCompartir.Clicked += async (s, e) =>
                await Share.Default.RequestAsync(
                    new ShareTextRequest { Text = textoOCR, Title = "Texto OCR" });

            var grid = new Grid
            {
                RowDefinitions = new RowDefinitionCollection
                {
                    new RowDefinition { Height = GridLength.Star },
                    new RowDefinition { Height = GridLength.Auto }
                }
            };
            grid.Children.Add(editor);
            grid.Children.Add(btnCompartir);
            Grid.SetRow(btnCompartir, 1);

            Content = grid;
        }
    }
}
