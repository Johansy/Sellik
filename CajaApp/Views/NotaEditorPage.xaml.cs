using CajaApp.Models;
using CajaApp.ViewModels;

namespace CajaApp.Views
{
    public partial class NotaEditorPage : ContentPage
    {
        private readonly TipoNota _tipoNota;
        private readonly Nota? _notaExistente;
        private readonly NotasViewModel _viewModel;
        private string? _rutaImagenTemporal;
        private bool _esEdicion;

        public NotaEditorPage(TipoNota tipoNota, Nota? notaExistente, NotasViewModel viewModel)
        {
            InitializeComponent();
            _tipoNota = tipoNota;
            _notaExistente = notaExistente;
            _viewModel = viewModel;
            _esEdicion = notaExistente != null;

            ConfigurarInterfaz();
            CargarDatos();
        }

        private void ConfigurarInterfaz()
        {
            // Configurar título y visibilidad según el tipo
            switch (_tipoNota)
            {
                case TipoNota.Texto:
                    TipoNotaLabel.Text = _esEdicion ? "✏️ Editar Nota de Texto" : "📝 Nueva Nota de Texto";
                    ContenidoLabel.IsVisible = true;
                    ContenidoEditor.IsVisible = true;
                    ImagenSection.IsVisible = false;
                    break;

                case TipoNota.Imagen:
                    TipoNotaLabel.Text = _esEdicion ? "✏️ Editar Nota con Imagen" : "📷 Nueva Nota con Imagen";
                    ContenidoLabel.IsVisible = false;
                    ContenidoEditor.IsVisible = false;
                    ImagenSection.IsVisible = true;
                    break;

                case TipoNota.TextoConImagen:
                    TipoNotaLabel.Text = _esEdicion ? "✏️ Editar Nota Mixta" : "📝📷 Nueva Nota Mixta";
                    ContenidoLabel.IsVisible = true;
                    ContenidoEditor.IsVisible = true;
                    ImagenSection.IsVisible = true;
                    break;
            }

            // Configurar fecha y hora actuales
            var ahora = DateTime.Now;
            FechaPicker.Date = ahora.Date;
            HoraPicker.Time = ahora.TimeOfDay;
        }

        private async void CargarDatos()
        {
            if (_notaExistente != null)
            {
                TituloEntry.Text = _notaExistente.Titulo;
                ContenidoEditor.Text = _notaExistente.Contenido;
                EtiquetasEntry.Text = _notaExistente.Etiquetas;
                FavoritaCheckBox.IsChecked = _notaExistente.EsFavorita;

                FechaPicker.Date = _notaExistente.Fecha.Date;
                HoraPicker.Time = _notaExistente.Fecha.TimeOfDay;

                // Cargar imagen si existe
                if (!string.IsNullOrEmpty(_notaExistente.RutaImagen))
                {
                    await CargarImagenExistente(_notaExistente.RutaImagen);
                }
            }
        }

        private async Task CargarImagenExistente(string rutaImagen)
        {
            try
            {
                var imagenBytes = await _viewModel.ObtenerImagenAsync(rutaImagen);
                if (imagenBytes != null)
                {
                    var stream = new MemoryStream(imagenBytes);
                    ImagenPreview.Source = ImageSource.FromStream(() => new MemoryStream(imagenBytes));
                    ImagenFrame.IsVisible = true;
                    _rutaImagenTemporal = rutaImagen;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo cargar la imagen: {ex.Message}", "OK");
            }
        }

        private async void OnTomarFotoClicked(object sender, EventArgs e)
        {
            try
            {
                var status = await Permissions.RequestAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    await DisplayAlert("Permisos", "Se necesita acceso a la cámara", "OK");
                    return;
                }

                var photo = await MediaPicker.Default.CapturePhotoAsync();
                if (photo != null)
                {
                    await ProcesarImagen(photo);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo tomar la foto: {ex.Message}", "OK");
            }
        }

        private async void OnSeleccionarImagenClicked(object sender, EventArgs e)
        {
            try
            {
                var photo = await MediaPicker.Default.PickPhotoAsync();
                if (photo != null)
                {
                    await ProcesarImagen(photo);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo seleccionar la imagen: {ex.Message}", "OK");
            }
        }

        private async Task ProcesarImagen(FileResult photo)
        {
            try
            {
                // 1. Leer todos los bytes mientras el stream está abierto
                byte[] imageBytes;
                using (var stream = await photo.OpenReadAsync())
                {
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    imageBytes = ms.ToArray();  // ✅ bytes seguros en memoria
                }

                // 2. Guardar la imagen desde los bytes (no desde el stream)
                _rutaImagenTemporal = await _viewModel.GuardarImagenAsync(imageBytes,
                    Path.GetExtension(photo.FileName));

                // 3. Crear un MemoryStream fresco en el factory (nunca se desecha prematuramente)
                ImagenPreview.Source = ImageSource.FromStream(() => new MemoryStream(imageBytes));
                ImagenFrame.IsVisible = true;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo procesar la imagen: {ex.Message}", "OK");
            }
        }

        private void OnEliminarImagenClicked(object sender, EventArgs e)
        {
            ImagenPreview.Source = null;
            ImagenFrame.IsVisible = false;
            _rutaImagenTemporal = null;
        }

        private async void OnGuardarClicked(object sender, EventArgs e)
        {
            try
            {
                if (!ValidarFormulario())
                    return;

                var nota = _notaExistente ?? new Nota();

                nota.Titulo = TituloEntry.Text?.Trim() ?? string.Empty;
                nota.Contenido = ContenidoEditor.Text?.Trim() ?? string.Empty;
                nota.Etiquetas = EtiquetasEntry.Text?.Trim() ?? string.Empty;
                nota.EsFavorita = FavoritaCheckBox.IsChecked;
                nota.Tipo = _tipoNota;
                nota.RutaImagen = !string.IsNullOrEmpty(_rutaImagenTemporal)
                    ? Path.GetFileName(_rutaImagenTemporal)
                    : string.Empty;

                // Combinar fecha y hora
                var fecha = FechaPicker.Date;
                var hora = HoraPicker.Time;
                nota.Fecha = fecha.Add(hora);

                if (!string.IsNullOrEmpty(_rutaImagenTemporal))
                {
                    nota.NombreArchivoImagen = Path.GetFileName(_rutaImagenTemporal);
                }

                bool resultado = await _viewModel.GuardarNotaAsync(nota);

                if (resultado)
                {
                    await DisplayAlert("Éxito", "Nota guardada correctamente", "OK");
                    await Navigation.PopAsync();
                }
                else
                {
                    await DisplayAlert("Error", "No se pudo guardar la nota", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error al guardar: {ex.Message}", "OK");
            }
        }

        private bool ValidarFormulario()
        {
            if (string.IsNullOrWhiteSpace(TituloEntry.Text))
            {
                DisplayAlert("Validación", "El título es obligatorio", "OK");
                return false;
            }

            if (_tipoNota == TipoNota.Texto && string.IsNullOrWhiteSpace(ContenidoEditor.Text))
            {
                DisplayAlert("Validación", "El contenido es obligatorio para notas de texto", "OK");
                return false;
            }

            if (_tipoNota == TipoNota.Imagen && string.IsNullOrWhiteSpace(_rutaImagenTemporal))
            {
                DisplayAlert("Validación", "Debe seleccionar una imagen", "OK");
                return false;
            }

            return true;
        }

        private async void OnCancelarClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Confirmar",
                "¿Está seguro de cancelar? Se perderán los cambios no guardados.",
                "Sí", "No");

            if (confirm)
            {
                // Limpiar imagen temporal si no es edición
                if (!_esEdicion && !string.IsNullOrEmpty(_rutaImagenTemporal))
                {
                    try
                    {
                        File.Delete(_rutaImagenTemporal);
                    }
                    catch { }
                }

                await Navigation.PopAsync();
            }
        }
    }
}