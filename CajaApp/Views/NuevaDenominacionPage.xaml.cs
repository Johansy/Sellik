using CajaApp.ViewModels;
using CajaApp.Models;

namespace CajaApp.Views
{
    public partial class NuevaDenominacionPage : ContentPage
    {
        private readonly ConfiguracionViewModel _viewModel;
        private readonly Dictionary<string, string> _coloresDisponibles;

        public NuevaDenominacionPage(ConfiguracionViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;

            _coloresDisponibles = new Dictionary<string, string>
            {
                ["Café (Monedas pequeñas)"] = "#8D6E63",
                ["Dorado (Monedas grandes)"] = "#FFB74D",
                ["Azul (Billetes)"] = "#1976D2",
                ["Rojo (Billetes)"] = "#D32F2F",
                ["Verde (Billetes)"] = "#388E3C",
                ["Naranja (Billetes)"] = "#F57C00",
                ["Morado (Billetes)"] = "#7B1FA2",
                ["Rosa (Billetes)"] = "#C2185B",
                ["Gris (Personalizado)"] = "#757575"
            };

            // Configurar eventos
            ValorEntry.TextChanged += OnFormularioChanged;
            SimboloEntry.TextChanged += OnFormularioChanged;
            TipoPicker.SelectedIndexChanged += OnFormularioChanged;
            ColorPicker.SelectedIndexChanged += OnFormularioChanged;

            // Configurar valores por defecto
            TipoPicker.SelectedIndex = 1; // Billete por defecto
            ColorPicker.SelectedIndex = 8; // Gris por defecto
        }

        private void OnFormularioChanged(object? sender, EventArgs e)
        {
            ActualizarVistaPrevia();
        }

        private void ActualizarVistaPrevia()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ValorEntry.Text) ||
                    string.IsNullOrWhiteSpace(SimboloEntry.Text) ||
                    TipoPicker.SelectedIndex == -1 ||
                    ColorPicker.SelectedIndex == -1)
                {
                    VistaPreviewFrame.IsVisible = false;
                    return;
                }

                // Actualizar vista previa
                PreviewLabel.Text = SimboloEntry.Text;

                var colorSeleccionado = ColorPicker.Items[ColorPicker.SelectedIndex];
                if (_coloresDisponibles.ContainsKey(colorSeleccionado))
                {
                    PreviewFrame.BackgroundColor = Color.FromArgb(_coloresDisponibles[colorSeleccionado]);
                }

                var tipo = TipoPicker.SelectedIndex == 0 ? "Moneda" : "Billete";
                DescripcionPreviewLabel.Text = $"{SimboloEntry.Text} - ${ValorEntry.Text} ({tipo})";

                VistaPreviewFrame.IsVisible = true;
            }
            catch
            {
                VistaPreviewFrame.IsVisible = false;
            }
        }

        private async void OnGuardarClicked(object sender, EventArgs e)
        {
            try
            {
                if (!ValidarFormulario())
                    return;

                if (!decimal.TryParse(ValorEntry.Text, out decimal valor) || valor <= 0)
                {
                    await DisplayAlert("Error", "El valor debe ser un número mayor a cero", "OK");
                    return;
                }

                var tipo = TipoPicker.SelectedIndex == 0 ? TipoDenominacion.Moneda : TipoDenominacion.Billete;
                var colorSeleccionado = ColorPicker.Items[ColorPicker.SelectedIndex];
                var color = _coloresDisponibles[colorSeleccionado];

                bool resultado = await _viewModel.AgregarDenominacionAsync(valor, SimboloEntry.Text.Trim(), tipo, color);

                if (resultado)
                {
                    await DisplayAlert("Éxito", "Denominación personalizada agregada correctamente", "OK");
                    await Navigation.PopAsync();
                }
                else
                {
                    await DisplayAlert("Error", "No se pudo agregar la denominación", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error al guardar: {ex.Message}", "OK");
            }
        }

        private bool ValidarFormulario()
        {
            if (string.IsNullOrWhiteSpace(ValorEntry.Text))
            {
                DisplayAlert("Validación", "El valor es obligatorio", "OK");
                return false;
            }

            if (string.IsNullOrWhiteSpace(SimboloEntry.Text))
            {
                DisplayAlert("Validación", "El símbolo es obligatorio", "OK");
                return false;
            }

            if (TipoPicker.SelectedIndex == -1)
            {
                DisplayAlert("Validación", "Debe seleccionar un tipo", "OK");
                return false;
            }

            if (ColorPicker.SelectedIndex == -1)
            {
                DisplayAlert("Validación", "Debe seleccionar un color", "OK");
                return false;
            }

            return true;
        }

        private async void OnCancelarClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}