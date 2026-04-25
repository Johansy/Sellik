using CajaApp.ViewModels;
using CajaApp.Models;
using CajaApp.Services;
using Microsoft.Maui.Controls;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace CajaApp.Views
{
    public partial class MovimientosPage : ContentPage
    {
        private readonly MovimientosViewModel _viewModel;
        private readonly ExportService _exportService;

        public MovimientosPage(MovimientosViewModel viewModel, ExportService exportService)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _exportService = exportService;
            BindingContext = _viewModel;

            // Configurar fecha actual
            FechaPicker.Date = DateTime.Today;

            // Poblar Picker de tipo con textos localizados
            PopularTipoPicker();

            // Suscribirse a cambios del picker de tipo
            TipoPicker.SelectedIndexChanged += OnTipoPickerChanged;

            // Configurar picker de tipo
            TipoPicker.SelectedIndex = 0; // Entrada por defecto

            // Actualizar el Picker cuando cambie el idioma
            LocalizationService.Instance.PropertyChanged += (_, _) =>
            {
                PopularTipoPicker();
                RefrescarConceptoPicker();
            };
        }

        private void PopularTipoPicker()
        {
            var L = LocalizationService.Instance;
            int selectedIndex = TipoPicker.SelectedIndex;
            TipoPicker.Items.Clear();
            TipoPicker.Items.Add(L["Mov_TipoEntrada"]);
            TipoPicker.Items.Add(L["Mov_TipoSalida"]);
            if (selectedIndex >= 0)
                TipoPicker.SelectedIndex = selectedIndex;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.CargarDatos();
        }

        private async void OnTipoPickerChanged(object? sender, EventArgs e)
        {
            if (TipoPicker.SelectedIndex == -1) return;

            ConceptoPicker.ItemsSource = null;
            ConceptoPicker.SelectedIndex = -1;

            if (TipoPicker.SelectedIndex == 0) // Entrada
            {
                ConceptoPicker.ItemsSource = _viewModel.ConceptosEntrada;
                ConceptoPicker.ItemDisplayBinding = new Binding("NombreLocalizado");
            }
            else // Salida
            {
                ConceptoPicker.ItemsSource = _viewModel.ConceptosSalida;
                ConceptoPicker.ItemDisplayBinding = new Binding("NombreLocalizado");
            }
        }

        private void RefrescarConceptoPicker()
        {
            // Fuerza al Picker a re-leer NombreLocalizado reasignando el ItemsSource
            if (ConceptoPicker.ItemsSource == null) return;
            var source = ConceptoPicker.ItemsSource;
            var index = ConceptoPicker.SelectedIndex;
            ConceptoPicker.ItemsSource = null;
            ConceptoPicker.ItemsSource = source;
            ConceptoPicker.SelectedIndex = index;
        }

        private async void OnAgregarClicked(object sender, EventArgs e)
        {
            if (!ValidarFormulario())
                return;

            try
            {
                var textoMonto = (MontoEntry.Text ?? "0").Trim();
                var montoNormalizado = textoMonto.Replace(',', '.');

                if (!decimal.TryParse(montoNormalizado, NumberStyles.Number, CultureInfo.InvariantCulture, out var monto))
                {
                    var L = LocalizationService.Instance;
                    await DisplayAlert(L["Lbl_Validacion"], L["Mov_MontoInvalido"], L["Btn_Aceptar"]);
                    return;
                }

                var movimiento = new MovimientoEfectivo
                {
                    Fecha = FechaPicker.Date,
                    Tipo = TipoPicker.SelectedIndex == 0 ? TipoMovimiento.Entrada : TipoMovimiento.Salida,
                    Monto = monto,
                    Concepto = ((ConceptoMovimiento)ConceptoPicker.SelectedItem)?.Nombre ?? "",
                    Descripcion = DescripcionEntry.Text?.Trim() ?? "",
                    Responsable = Environment.UserName
                };

                bool resultado = await _viewModel.GuardarMovimientoAsync(movimiento);

                if (resultado)
                {
                    var L = LocalizationService.Instance;
                    await DisplayAlert(L["Lbl_Exito"], L["Mov_ExitoGuardar"], L["Btn_Aceptar"]);
                    LimpiarFormulario();
                }
                else
                {
                    var L = LocalizationService.Instance;
                    await DisplayAlert(L["Lbl_Error"], L["Mov_ErrorGuardar"], L["Btn_Aceptar"]);
                }
            }
            catch (Exception ex)
            {
                var L = LocalizationService.Instance;
                await DisplayAlert(L["Lbl_Error"], LocalizationService.GetF("Lbl_ErrorProcesar", ex.Message), L["Btn_Aceptar"]);
            }
        }

        private bool ValidarFormulario()
        {
            var L = LocalizationService.Instance;

            if (TipoPicker.SelectedIndex == -1)
            {
                DisplayAlert(L["Lbl_Validacion"], L["Mov_ValidarTipo"], L["Btn_Aceptar"]);
                return false;
            }

            if (ConceptoPicker.SelectedIndex == -1)
            {
                DisplayAlert(L["Lbl_Validacion"], L["Mov_ValidarConcepto"], L["Btn_Aceptar"]);
                return false;
            }

            if (string.IsNullOrWhiteSpace(MontoEntry.Text) ||
                !decimal.TryParse(MontoEntry.Text, out decimal monto) ||
                monto <= 0)
            {
                DisplayAlert(L["Lbl_Validacion"], L["Mov_MontoInvalido"], L["Btn_Aceptar"]);
                return false;
            }

            return true;
        }

        private void LimpiarFormulario()
        {
            MontoEntry.Text = string.Empty;
            DescripcionEntry.Text = string.Empty;
            ConceptoPicker.SelectedIndex = -1;
            FechaPicker.Date = DateTime.Today;
        }

        private void OnLimpiarFormularioClicked(object sender, EventArgs e)
        {
            LimpiarFormulario();
        }

        private async void OnEliminarClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is MovimientoEfectivo movimiento)
            {
                var L = LocalizationService.Instance;
                bool confirm = await DisplayAlert(L["Lbl_Confirmar"],
                    $"{L["Mov_ConfirmarEliminar"]}\n\n"
                    + $"{movimiento.TipoTexto}: {movimiento.MontoTexto}\n"
                    + $"{movimiento.Concepto}\n"
                    + $"{movimiento.Fecha:dd/MM/yyyy}",
                    L["Btn_Eliminar"], L["Btn_Cancelar"]);

                if (confirm)
                {
                    bool resultado = await _viewModel.EliminarMovimientoAsync(movimiento);

                    if (resultado)
                    {
                        await DisplayAlert(L["Lbl_Exito"], L["Mov_Eliminado"], L["Btn_Aceptar"]);
                    }
                    else
                    {
                        await DisplayAlert(L["Lbl_Error"], L["Mov_ErrorEliminar"], L["Btn_Aceptar"]);
                    }
                }
            }
        }

        private async void OnGenerarReporteClicked(object sender, EventArgs e)
        {
            try
            {
                var L = LocalizationService.Instance;

                string hoy      = L["Mov_PeriodoHoy"];
                string semana   = L["Mov_PeriodoSemana"];
                string mes      = L["Mov_PeriodoMes"];
                string ultimos  = L["Mov_Periodo30"];
                string todo     = L["Mov_PeriodoTodo"];
                string cancelar = L["Btn_Cancelar"];

                string periodo = await DisplayActionSheet(
                    L["Mov_PeriodoTitulo"], cancelar, null,
                    hoy, semana, mes, ultimos, todo);

                if (periodo == cancelar || string.IsNullOrEmpty(periodo))
                    return;

                DateTime fechaInicio, fechaFin;

                if (periodo == hoy)
                {
                    fechaInicio = DateTime.Today;
                    fechaFin = DateTime.Today.AddDays(1).AddTicks(-1);
                }
                else if (periodo == semana)
                {
                    fechaInicio = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                    fechaFin = fechaInicio.AddDays(7).AddTicks(-1);
                }
                else if (periodo == mes)
                {
                    fechaInicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    fechaFin = fechaInicio.AddMonths(1).AddTicks(-1);
                }
                else if (periodo == ultimos)
                {
                    fechaInicio = DateTime.Today.AddDays(-30);
                    fechaFin = DateTime.Today.AddDays(1).AddTicks(-1);
                }
                else // Todo
                {
                    fechaInicio = DateTime.MinValue;
                    fechaFin = DateTime.MaxValue;
                }

                string reporte = _viewModel.GenerarReporteTexto(fechaInicio, fechaFin);

                string verReporte      = L["Mov_VerReporte"];
                string compartirReporte = L["Mov_CompartirReporte"];

                string accion = await DisplayActionSheet(
                    L["Mov_AccionReporte"], cancelar, null,
                    verReporte, compartirReporte);

                if (accion == verReporte)
                {
                    await DisplayAlert($"{L["Mov_BtnReporte"]} - {periodo}", reporte, L["Btn_Aceptar"]);
                }
                else if (accion == compartirReporte)
                {
                    await Share.Default.RequestAsync(new ShareTextRequest
                    {
                        Text = reporte,
                        Title = $"{L["Mov_BtnReporte"]} - {periodo}"
                    });
                }
            }
            catch (Exception ex)
            {
                var L = LocalizationService.Instance;
                await DisplayAlert(L["Lbl_Error"], LocalizationService.GetF("Lbl_ErrorProcesar", ex.Message), L["Btn_Aceptar"]);
            }
        }

        private async void OnCompartirClicked(object sender, EventArgs e)
        {
            try
            {
                var L = LocalizationService.Instance;
                string reporte = _viewModel.GenerarReporteTexto(DateTime.MinValue, DateTime.MaxValue);

                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Text = reporte,
                    Title = L["Mov_TituloCompartir"]
                });
            }
            catch (Exception ex)
            {
                var L = LocalizationService.Instance;
                await DisplayAlert(L["Lbl_Error"], LocalizationService.GetF("Lbl_ErrorProcesar", ex.Message), L["Btn_Aceptar"]);
            }
        }

        private void OnFiltroRapidoClicked(object sender, EventArgs e)
        {
            if (sender is not Button btn) return;

            var filtro = btn.CommandParameter?.ToString();

            // Resaltar botón activo
            Button[] botones = { BtnTodo, BtnHoy, BtnSemana, BtnMes, BtnAnio };
            foreach (var b in botones)
            {
                b.BackgroundColor = Color.FromArgb("#EEEEEE");
                b.TextColor = Colors.Black;
            }
            btn.BackgroundColor = Color.FromArgb("#2E7D32");
            btn.TextColor = Colors.White;

            _viewModel.FiltroActivo = filtro switch
            {
                "Hoy"   => FiltroMovimiento.Hoy,
                "Semana" => FiltroMovimiento.EstaSemana,
                "Mes"   => FiltroMovimiento.EsteMes,
                "Anio"  => FiltroMovimiento.EsteAnio,
                _       => FiltroMovimiento.Todo
            };
        }

        private void OnFiltroFechaSeleccionada(object sender, DateChangedEventArgs e)
        {
            _viewModel.FechaFiltro = e.NewDate;
            _viewModel.FiltroActivo = FiltroMovimiento.FechaEspecifica;

            // Quitar resaltado de botones rápidos
            Button[] botones = { BtnTodo, BtnHoy, BtnSemana, BtnMes, BtnAnio };
            foreach (var b in botones)
            {
                b.BackgroundColor = Color.FromArgb("#EEEEEE");
                b.TextColor = Colors.Black;
            }
        }

        private async void OnExportarExcelClicked(object sender, EventArgs e)
        {
            try
            {
                var L = LocalizationService.Instance;
                var movimientos = _viewModel.ObtenerMovimientosFiltrados();
                if (!movimientos.Any())
                {
                    await DisplayAlert(L["Lbl_SinDatos"], L["Mov_ExportarSinDatos"], L["Btn_Aceptar"]);
                    return;
                }

                _viewModel.IsLoading = true;
                var ruta = await Task.Run(() => _exportService.GenerarExcelMovimientos(movimientos));
                _viewModel.IsLoading = false;

                await _exportService.CompartirArchivoAsync(ruta, "Movimientos.xlsx");
            }
            catch (Exception ex)
            {
                _viewModel.IsLoading = false;
                var L = LocalizationService.Instance;
                await DisplayAlert(L["Lbl_Error"], LocalizationService.GetF("Mov_ExportarError", ex.Message), L["Btn_Aceptar"]);
            }
        }

        private async void OnExportarPdfClicked(object sender, EventArgs e)
        {
            try
            {
                var L = LocalizationService.Instance;
                var movimientos = _viewModel.ObtenerMovimientosFiltrados();
                if (!movimientos.Any())
                {
                    await DisplayAlert(L["Lbl_SinDatos"], L["Mov_ExportarSinDatos"], L["Btn_Aceptar"]);
                    return;
                }

                _viewModel.IsLoading = true;
                var ruta = await Task.Run(() => _exportService.GenerarPdfMovimientos(movimientos));
                _viewModel.IsLoading = false;

                await _exportService.CompartirArchivoAsync(ruta, "Movimientos.pdf");
            }
            catch (PlatformNotSupportedException pex)
            {
                _viewModel.IsLoading = false;
                var L = LocalizationService.Instance;
                await DisplayAlert(L["Mov_ExportarNoDisponible"], pex.Message, L["Btn_Aceptar"]);
            }
            catch (Exception ex)
            {
                _viewModel.IsLoading = false;
                var L = LocalizationService.Instance;
                await DisplayAlert(L["Lbl_Error"], LocalizationService.GetF("Mov_ExportarError", ex.Message), L["Btn_Aceptar"]);
            }
        }
    }
}