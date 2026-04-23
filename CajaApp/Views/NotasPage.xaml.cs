using CajaApp.ViewModels;
using CajaApp.Models;
using CajaApp.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using System;
using System.Text;
using System.Threading.Tasks;

namespace CajaApp.Views
{
    public partial class NotasPage : ContentPage
    {
        private readonly NotasViewModel _viewModel;

        public NotasPage(NotasViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;

            PopularFiltroTipoPicker();
            FiltroTipoPicker.SelectedIndex = 0;
            FechaEspecificaPicker.MaximumDate = DateTime.Today;
            FechaEspecificaPicker.Date = DateTime.Today;

            LocalizationService.Instance.PropertyChanged += (_, _) => PopularFiltroTipoPicker();
        }

        private void PopularFiltroTipoPicker()
        {
            var L = LocalizationService.Instance;
            int idx = FiltroTipoPicker.SelectedIndex;
            FiltroTipoPicker.Items.Clear();
            FiltroTipoPicker.Items.Add(L["Notas_FiltroTodos"]);
            FiltroTipoPicker.Items.Add(L["Notas_FiltroTexto"]);
            FiltroTipoPicker.Items.Add(L["Notas_FiltroImagen"]);
            FiltroTipoPicker.Items.Add(L["Notas_FiltroMixta"]);
            if (idx >= 0) FiltroTipoPicker.SelectedIndex = idx;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.CargarNotas();
        }

        private async void OnNuevaNotaTextoClicked(object sender, EventArgs e)
        {
            await MostrarEditorNota(TipoNota.Texto);
        }

        private async void OnNuevaNotaImagenClicked(object sender, EventArgs e)
        {
            await MostrarEditorNota(TipoNota.Imagen);
        }

        private async void OnNuevaNotaMixtaClicked(object sender, EventArgs e)
        {
            await MostrarEditorNota(TipoNota.TextoConImagen);
        }

        private async Task MostrarEditorNota(TipoNota tipo, Nota? notaExistente = null)
        {
            var editorPage = new NotaEditorPage(tipo, notaExistente, _viewModel);
            await Navigation.PushAsync(editorPage);
        }

        private void OnBuscarTextChanged(object sender, TextChangedEventArgs e)
        {
            // El binding se encarga automáticamente del filtrado
        }

        private void OnBuscarPressed(object sender, EventArgs e)
        {
            // Cerrar teclado
            BuscarEntry.Unfocus();
        }

        private void OnFiltroTipoChanged(object sender, EventArgs e)
        {
            var picker = sender as Picker;
            if (picker?.SelectedIndex >= 0)
            {
                _viewModel.FiltroTipo = picker.SelectedIndex switch
                {
                    0 => null, // Todos
                    1 => TipoNota.Texto,
                    2 => TipoNota.Imagen,
                    3 => TipoNota.TextoConImagen,
                    _ => null
                };
            }
        }

        private void OnLimpiarFiltrosClicked(object sender, EventArgs e)
        {
            _viewModel.LimpiarFiltros();
            FiltroTipoPicker.SelectedIndex = 0;
            FavoritasCheckBox.IsChecked = false;
            BuscarEntry.Text = "";
            FechaEspecificaPicker.Date = DateTime.Today;
            ActualizarBotonesFecha(0);
        }

        private void OnFiltroFechaClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.CommandParameter?.ToString(), out int indice))
            {
                _viewModel.FiltroFechaIndice = indice;
                ActualizarBotonesFecha(indice);
            }
        }

        private void OnFechaEspecificaSelected(object sender, DateChangedEventArgs e)
        {
            _viewModel.FechaEspecifica = e.NewDate;
            ActualizarBotonesFecha(5);
        }

        private void ActualizarBotonesFecha(int indiceActivo)
        {
            var activo = "#9C27B0";
            var inactivo = (Color)Application.Current!.Resources["CardColor"];
            var textoActivo = Colors.White;
            var textoInactivo = (Color)Application.Current!.Resources["TextColor"];

            BtnFechaTodas.BackgroundColor   = indiceActivo == 0 ? Color.FromArgb(activo) : inactivo;
            BtnFechaTodas.TextColor         = indiceActivo == 0 ? textoActivo : textoInactivo;
            BtnFechaHoy.BackgroundColor     = indiceActivo == 1 ? Color.FromArgb(activo) : inactivo;
            BtnFechaHoy.TextColor           = indiceActivo == 1 ? textoActivo : textoInactivo;
            BtnFechaSemana.BackgroundColor  = indiceActivo == 2 ? Color.FromArgb(activo) : inactivo;
            BtnFechaSemana.TextColor        = indiceActivo == 2 ? textoActivo : textoInactivo;
            BtnFechaMes.BackgroundColor     = indiceActivo == 3 ? Color.FromArgb(activo) : inactivo;
            BtnFechaMes.TextColor           = indiceActivo == 3 ? textoActivo : textoInactivo;
            BtnFechaAnio.BackgroundColor    = indiceActivo == 4 ? Color.FromArgb(activo) : inactivo;
            BtnFechaAnio.TextColor          = indiceActivo == 4 ? textoActivo : textoInactivo;
        }

        private async void OnVerNotaClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is Nota nota)
            {
                await MostrarDetalleNota(nota);
            }
        }

        private async void OnEditarNotaClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is Nota nota)
            {
                await MostrarEditorNota(nota.Tipo, nota);
            }
        }

        private async void OnToggleFavoritaClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is Nota nota)
            {
                bool resultado = await _viewModel.CambiarFavoritaAsync(nota);
                if (!resultado)
                {
                    var L = LocalizationService.Instance;
                    await DisplayAlert(L["Lbl_Error"], L["Notas_Eliminada"], L["Btn_Aceptar"]);
                }
            }
        }

        private async void OnEliminarNotaClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is Nota nota)
            {
                var L = LocalizationService.Instance;
                bool confirm = await DisplayAlert(L["Lbl_Confirmar"],
                    $"{L["Notas_ConfirmarEliminar"]}\n\n"
                    + $"{L["Notas_SinTitulo"].Replace("Sin t", "T")}: {nota.Titulo ?? L["Notas_SinTitulo"]}\n"
                    + $"Fecha: {nota.FechaTexto}\n"
                    + (nota.TieneImagen ? L["Notas_TieneImagen"] : ""),
                    L["Btn_Eliminar"], L["Btn_Cancelar"]);

                if (confirm)
                {
                    bool resultado = await _viewModel.EliminarNotaAsync(nota);

                    if (resultado)
                    {
                        await DisplayAlert(L["Lbl_Exito"], L["Notas_EliminadaOk"], L["Btn_Aceptar"]);
                    }
                    else
                    {
                        await DisplayAlert(L["Lbl_Error"], L["Notas_Eliminada"], L["Btn_Aceptar"]);
                    }
                }
            }
        }

        private async Task MostrarDetalleNota(Nota nota)
        {
            var detallePage = new NotaDetallePage(nota, _viewModel);
            await Navigation.PushAsync(detallePage);
        }

        private async void OnExportarClicked(object sender, EventArgs e)
        {
            try
            {
                var L = LocalizationService.Instance;
                string soloTexto = L["Notas_ExportarSoloTexto"];
                string completo  = L["Notas_ExportarCompleto"];
                string backup    = L["Notas_BackupCompleto"];
                string cancelar  = L["Btn_Cancelar"];

                string formato = await DisplayActionSheet(L["Notas_ExportarTitulo"], cancelar, null,
                    soloTexto, completo, backup);

                if (formato == cancelar || string.IsNullOrEmpty(formato)) return;

                string contenido = "";
                string titulo = "";

                if (formato == soloTexto)
                {
                    contenido = GenerarExportacionTexto();
                    titulo = soloTexto;
                }
                else if (formato == completo)
                {
                    contenido = GenerarExportacionCompleta();
                    titulo = completo;
                }
                else if (formato == backup)
                {
                    await GenerarBackupCompleto();
                    return;
                }

                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Text = contenido,
                    Title = titulo
                });
            }
            catch (Exception ex)
            {
                var L = LocalizationService.Instance;
                await DisplayAlert(L["Lbl_Error"], LocalizationService.GetF("Lbl_ErrorProcesar", ex.Message), L["Btn_Aceptar"]);
            }
        }

        private string GenerarExportacionTexto()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== EXPORTACIÓN DE NOTAS ===");
            sb.AppendLine($"Fecha de exportación: {DateTime.Now:dd/MM/yyyy HH:mm}");
            sb.AppendLine($"Total de notas: {_viewModel.TotalNotas}");
            sb.AppendLine();

            foreach (var nota in _viewModel.Notas)
            {
                sb.AppendLine($"=== {nota.Titulo ?? "Sin título"} ===");
                sb.AppendLine($"Fecha: {nota.FechaTexto}");
                sb.AppendLine($"Tipo: {nota.TipoTexto}");
                if (nota.EsFavorita) sb.AppendLine("⭐ FAVORITA");
                sb.AppendLine();

                if (!string.IsNullOrEmpty(nota.Contenido))
                {
                    sb.AppendLine("CONTENIDO:");
                    sb.AppendLine(nota.Contenido);
                    sb.AppendLine();
                }

                if (!string.IsNullOrEmpty(nota.Etiquetas))
                {
                    sb.AppendLine($"Etiquetas: {nota.Etiquetas}");
                    sb.AppendLine();
                }

                sb.AppendLine("------------------------");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string GenerarExportacionCompleta()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== EXPORTACIÓN COMPLETA DE NOTAS ===");
            sb.AppendLine($"Fecha de exportación: {DateTime.Now:dd/MM/yyyy HH:mm}");
            sb.AppendLine($"Total de notas: {_viewModel.TotalNotas}");
            sb.AppendLine();

            foreach (var nota in _viewModel.Notas)
            {
                sb.AppendLine($"ID: {nota.Id}");
                sb.AppendLine($"Título: {nota.Titulo ?? "Sin título"}");
                sb.AppendLine($"Fecha: {nota.FechaTexto}");
                sb.AppendLine($"Tipo: {nota.TipoTexto}");
                sb.AppendLine($"Favorita: {(nota.EsFavorita ? "Sí" : "No")}");

                if (!string.IsNullOrEmpty(nota.Contenido))
                {
                    sb.AppendLine("Contenido:");
                    sb.AppendLine(nota.Contenido);
                }

                if (!string.IsNullOrEmpty(nota.RutaImagen))
                {
                    sb.AppendLine($"Imagen: {nota.RutaImagen}");
                }

                if (!string.IsNullOrEmpty(nota.Etiquetas))
                {
                    sb.AppendLine($"Etiquetas: {nota.Etiquetas}");
                }

                sb.AppendLine($"Fecha creación: {nota.FechaCreacion:dd/MM/yyyy HH:mm}");
                sb.AppendLine($"Última modificación: {nota.FechaModificacion:dd/MM/yyyy HH:mm}");
                sb.AppendLine("========================");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private async Task GenerarBackupCompleto()
        {
            try
            {
                var L = LocalizationService.Instance;
                await DisplayAlert(L["Notas_BackupCompleto"],
                    "The full backup would include:\n"
                    + "- Export of all notes\n"
                    + "- Copy of all images\n"
                    + "- Full database\n\n"
                    + "This feature is pending implementation.",
                    L["Btn_Aceptar"]);
            }
            catch (Exception ex)
            {
                var L = LocalizationService.Instance;
                await DisplayAlert(L["Lbl_Error"], LocalizationService.GetF("Lbl_ErrorProcesar", ex.Message), L["Btn_Aceptar"]);
            }
        }

        private async void OnGenerarReporteClicked(object sender, EventArgs e)
        {
            try
            {
                var L = LocalizationService.Instance;
                string reporte = _viewModel.GenerarReporteNotas();

                string verReporte      = L["Notas_VerReporte"];
                string compartirReporte = L["Notas_CompartirReporte"];
                string cancelar        = L["Btn_Cancelar"];

                string accion = await DisplayActionSheet(L["Notas_AccionReporte"], cancelar, null,
                    verReporte, compartirReporte);

                if (accion == verReporte)
                    await DisplayAlert(L["Notas_TituloReporte"], reporte, L["Btn_Aceptar"]);
                else if (accion == compartirReporte)
                    await Share.Default.RequestAsync(new ShareTextRequest { Text = reporte, Title = L["Notas_TituloReporte"] });
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
                string reporte = _viewModel.GenerarReporteNotas();
                await Share.Default.RequestAsync(new ShareTextRequest { Text = reporte, Title = L["Notas_TituloCompartir"] });
            }
            catch (Exception ex)
            {
                var L = LocalizationService.Instance;
                await DisplayAlert(L["Lbl_Error"], LocalizationService.GetF("Lbl_ErrorProcesar", ex.Message), L["Btn_Aceptar"]);
            }
        }
    }
}