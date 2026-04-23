using CajaApp.ViewModels;
using CajaApp.Models;
using CajaApp.Services;

namespace CajaApp.Views
{
    public partial class ConfiguracionPage : ContentPage
    {
        private readonly ConfiguracionViewModel _viewModel;
        private readonly LocalizationService _loc = LocalizationService.Instance;

        public ConfiguracionPage(ConfiguracionViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                // Primera visita: inicializa configuración completa (seed + carga)
                // Visitas posteriores: solo refresca denominaciones (por si se agregó/eliminó alguna)
                await _viewModel.InicializarAsync();
                if (_viewModel.TotalDenominaciones == 0)
                    await _viewModel.CargarDenominaciones();
                PopularPickers();
                _loc.PropertyChanged += OnIdiomaChanged;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar Configuración: {ex}");
                await DisplayAlert(_loc["Lbl_Error"], _loc["Lbl_ErrorCargar"], _loc["Btn_Aceptar"]);
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _loc.PropertyChanged -= OnIdiomaChanged;
        }

        private void OnIdiomaChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            PopularPickers();
        }

        private void PopularPickers()
        {
            // OCR picker
            int ocrIdx = ModoOCRPicker.SelectedIndex < 0 ? _viewModel.ModoOCRIndex : ModoOCRPicker.SelectedIndex;
            ModoOCRPicker.Items.Clear();
            ModoOCRPicker.Items.Add(_loc["Config_OCRModoAuto"]);
            ModoOCRPicker.Items.Add(_loc["Config_OCRModoNativo"]);
            ModoOCRPicker.Items.Add(_loc["Config_OCRModoGPT"]);
            ModoOCRPicker.Items.Add(_loc["Config_OCRModoGoogle"]);
            ModoOCRPicker.Items.Add(_loc["Config_OCRModoNativoGPT"]);
            ModoOCRPicker.Items.Add(_loc["Config_OCRModoFusion"]);
            ModoOCRPicker.SelectedIndex = ocrIdx;

            // Tema picker
            int temaIdx = TemaPicker.SelectedIndex < 0 ? _viewModel.TemaSeleccionadoIndex : TemaPicker.SelectedIndex;
            TemaPicker.Items.Clear();
            TemaPicker.Items.Add(_loc["Config_TemaClaro"]);
            TemaPicker.Items.Add(_loc["Config_TemaOscuro"]);
            TemaPicker.Items.Add(_loc["Config_TemaAuto"]);
            TemaPicker.SelectedIndex = temaIdx;
        }

        private async void OnAgregarDenominacionClicked(object sender, EventArgs e)
        {
            try
            {
                // Crear formulario para nueva denominación
                var nuevaDenomPage = new NuevaDenominacionPage(_viewModel);
                await Navigation.PushAsync(nuevaDenomPage);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo abrir el formulario: {ex.Message}", "OK");
            }
        }

        private async void OnDenominacionToggled(object sender, ToggledEventArgs e)
        {
            try
            {
                if (sender is Switch switchControl &&
                    switchControl.BindingContext is DenominacionConfig denominacion)
                {
                    // Ignorar cambios programáticos (reciclado de celdas del CollectionView
                    // o actualización del binding OneWay): solo actuar cuando el usuario
                    // realmente cambia el estado.
                    if (e.Value == denominacion.EstaActiva)
                        return;

                    bool resultado = await _viewModel.CambiarEstadoDenominacionAsync(denominacion, e.Value);

                    if (!resultado)
                    {
                        // Revertir el switch si falló
                        switchControl.IsToggled = !e.Value;
                        await DisplayAlert(_loc["Lbl_Error"], _loc["Config_ErrorCambiarEstado"], _loc["Btn_Aceptar"]);
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert(_loc["Lbl_Error"], $"{ex.Message}", _loc["Btn_Aceptar"]);
            }
        }

        private async void OnEliminarDenominacionClicked(object sender, EventArgs e)
        {
            try
            {
                if (sender is Button button && button.CommandParameter is DenominacionConfig denominacion)
                {
                    if (!denominacion.EsPersonalizada)
                    {
                        await DisplayAlert(_loc["Lbl_Informacion"],
                            _loc["Config_ElimDenomNoPredeterminada"], _loc["Btn_Aceptar"]);
                        return;
                    }

                    bool confirm = await DisplayAlert(_loc["Config_ElimDenomTitulo"],
                        string.Format(_loc["Config_ElimDenomConfirmar"], denominacion.DescripcionCompleta),
                        _loc["Btn_Eliminar"], _loc["Btn_Cancelar"]);

                    if (confirm)
                    {
                        bool resultado = await _viewModel.EliminarDenominacionAsync(denominacion);

                        if (resultado)
                        {
                            await DisplayAlert(_loc["Lbl_Exito"], _loc["Config_ElimDenomOK"], _loc["Btn_Aceptar"]);
                        }
                        else
                        {
                            await DisplayAlert(_loc["Lbl_Error"], _loc["Config_ElimDenomError"], _loc["Btn_Aceptar"]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert(_loc["Lbl_Error"], $"{ex.Message}", _loc["Btn_Aceptar"]);
            }
        }

        private async void OnExportarClicked(object sender, EventArgs e)
        {
            try
            {
                string opcion = await DisplayActionSheet(
                    _loc["Config_ExportarTitulo"], _loc["Btn_Cancelar"], null,
                    _loc["Config_ExportarVerResumen"], _loc["Config_ExportarCompleto"], _loc["Config_ExportarCompartir"]);

                if (opcion == _loc["Btn_Cancelar"] || opcion == null) return;

                if (opcion == _loc["Config_ExportarVerResumen"])
                {
                    string resumen = _viewModel.GenerarResumenConfiguracion();
                    await DisplayAlert(_loc["Config_ExportarTitulo"], resumen, _loc["Btn_Aceptar"]);
                }
                else if (opcion == _loc["Config_ExportarCompleto"])
                {
                    await _viewModel.ExportarConfiguracionAsync();
                    await Share.Default.RequestAsync(new ShareTextRequest
                    {
                        Title = "Configuración Sellik"
                    });
                }
                else if (opcion == _loc["Config_ExportarCompartir"])
                {
                    string configuracionCompartir = _viewModel.GenerarResumenConfiguracion();
                    await Share.Default.RequestAsync(new ShareTextRequest
                    {
                        Text = configuracionCompartir,
                        Title = "Mi Configuración Sellik"
                    });
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert(_loc["Lbl_Error"], LocalizationService.GetF("Config_ExportarError", ex.Message), _loc["Btn_Aceptar"]);
            }
        }

        private async void OnRestaurarClicked(object sender, EventArgs e)
        {
            try
            {
                bool confirm = await DisplayAlert(_loc["Config_RestaurarTitulo"],
                    _loc["Config_RestaurarDetalle"],
                    _loc["Config_BtnRestaurar"], _loc["Btn_Cancelar"]);

                if (confirm)
                {
                    bool resultado = await _viewModel.RestaurarConfiguracionAsync();

                    if (resultado)
                    {
                        await DisplayAlert(_loc["Lbl_Exito"], _loc["Config_RestauradaOK"], _loc["Btn_Aceptar"]);
                    }
                    else
                    {
                        await DisplayAlert(_loc["Lbl_Error"], _loc["Config_RestaurarError"], _loc["Btn_Aceptar"]);
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert(_loc["Lbl_Error"], $"{ex.Message}", _loc["Btn_Aceptar"]);
            }
        }

        private async void OnAcercaDeClicked(object sender, EventArgs e)
        {
            try
            {
                var acercaDePage = new AcercaDePage();
                await Navigation.PushAsync(acercaDePage);
            }
            catch (Exception ex)
            {
                await DisplayAlert(_loc["Lbl_Error"], $"{ex.Message}", _loc["Btn_Aceptar"]);
            }
        }
    }
}