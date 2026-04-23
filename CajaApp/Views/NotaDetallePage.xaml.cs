using CajaApp.Models;
using CajaApp.ViewModels;
using System.Text;

namespace CajaApp.Views
{
    public partial class NotaDetallePage : ContentPage
    {
        private readonly Nota _nota;
        private readonly NotasViewModel _viewModel;

        public NotaDetallePage(Nota nota, NotasViewModel viewModel)
        {
            InitializeComponent();
            _nota = nota;
            _viewModel = viewModel;

            CargarDatos();
        }

        private async void CargarDatos()
        {
            try
            {
                // Información básica
                TituloLabel.Text = _nota.Titulo ?? "Sin título";
                TipoFechaLabel.Text = $"{_nota.TipoTexto} • {_nota.FechaTexto}";
                FavoritaLabel.Text = _nota.IconoFavorita;

                // Contenido
                if (!string.IsNullOrEmpty(_nota.Contenido))
                {
                    ContenidoLabel.Text = _nota.Contenido;
                    ContenidoFrame.IsVisible = true;
                }

                // Etiquetas
                if (!string.IsNullOrEmpty(_nota.Etiquetas))
                {
                    EtiquetasLabel.Text = _nota.Etiquetas;
                    EtiquetasFrame.IsVisible = true;
                }

                // Imagen
                if (!string.IsNullOrEmpty(_nota.RutaImagen))
                {
                    await CargarImagen();
                }

                // Fechas
                FechaCreacionLabel.Text = _nota.FechaCreacion.ToString("dd/MM/yyyy HH:mm");
                FechaModificacionLabel.Text = _nota.FechaModificacion.ToString("dd/MM/yyyy HH:mm");

                // Configurar botón favorita
                ActualizarBotonFavorita();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error cargando datos: {ex.Message}", "OK");
            }
        }

        private async Task CargarImagen()
        {
            try
            {
                var imagenBytes = await _viewModel.ObtenerImagenAsync(_nota.RutaImagen);
                if (imagenBytes != null)
                {
                    ImagenDetalle.Source = ImageSource.FromStream(() => new MemoryStream(imagenBytes));
                    ImagenFrame.IsVisible = true;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo cargar la imagen: {ex.Message}", "OK");
            }
        }

        private void ActualizarBotonFavorita()
        {
            FavoritaButton.Text = _nota.EsFavorita ? "⭐ Quitar de favoritas" : "☆ Agregar a favoritas";
        }

        private async void OnVerImagenCompletaClicked(object sender, EventArgs e)
        {
            try
            {
                var imagenPage = new ImagenCompletaPage(_nota.RutaImagen, _viewModel);
                await Navigation.PushAsync(imagenPage);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo mostrar la imagen: {ex.Message}", "OK");
            }
        }

        private async void OnEditarClicked(object sender, EventArgs e)
        {
            try
            {
                var editorPage = new NotaEditorPage(_nota.Tipo, _nota, _viewModel);
                await Navigation.PushAsync(editorPage);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo abrir el editor: {ex.Message}", "OK");
            }
        }

        private async void OnCompartirClicked(object sender, EventArgs e)
        {
            try
            {
                string contenido = GenerarContenidoParaCompartir();

                if (_nota.TieneImagen)
                {
                    var imagenBytes = await _viewModel.ObtenerImagenAsync(_nota.RutaImagen);
                    if (imagenBytes != null)
                    {
                        string tempDir = Path.Combine(FileSystem.CacheDirectory, "SharedTemp");
                        Directory.CreateDirectory(tempDir);

                        string extension = Path.GetExtension(_nota.RutaImagen);
                        if (string.IsNullOrEmpty(extension)) extension = ".jpg";
                        string tempImagePath = Path.Combine(tempDir, $"nota_imagen{extension}");
                        await File.WriteAllBytesAsync(tempImagePath, imagenBytes);

                        string tempTextPath = Path.Combine(tempDir, "nota_contenido.txt");
                        await File.WriteAllTextAsync(tempTextPath, contenido);

                        await Share.Default.RequestAsync(new ShareMultipleFilesRequest
                        {
                            Title = _nota.Titulo ?? "Mi Nota",
                            Files = new List<ShareFile>
                            {
                                new ShareFile(tempImagePath),
                                new ShareFile(tempTextPath)
                            }
                        });
                        return;
                    }
                }

                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Text = contenido,
                    Title = _nota.Titulo ?? "Mi Nota"
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo compartir: {ex.Message}", "OK");
            }
        }

        private string GenerarContenidoParaCompartir()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"📝 {_nota.Titulo ?? "Mi Nota"}");
            sb.AppendLine($"📅 {_nota.FechaTexto}");
            sb.AppendLine($"🏷️ {_nota.TipoTexto}");

            if (_nota.EsFavorita)
                sb.AppendLine("⭐ Favorita");

            sb.AppendLine();

            if (!string.IsNullOrEmpty(_nota.Contenido))
            {
                sb.AppendLine("CONTENIDO:");
                sb.AppendLine(_nota.Contenido);
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(_nota.Etiquetas))
            {
                sb.AppendLine($"Etiquetas: {_nota.Etiquetas}");
            }

            if (_nota.TieneImagen)
            {
                sb.AppendLine("📷 Esta nota incluye una imagen");
            }

            sb.AppendLine();
            sb.AppendLine($"Creada: {_nota.FechaCreacion:dd/MM/yyyy HH:mm}");
            sb.AppendLine("Enviada desde CajaApp");

            return sb.ToString();
        }

        private async void OnToggleFavoritaClicked(object sender, EventArgs e)
        {
            try
            {
                bool resultado = await _viewModel.CambiarFavoritaAsync(_nota);

                if (resultado)
                {
                    ActualizarBotonFavorita();
                    FavoritaLabel.Text = _nota.IconoFavorita;

                    string mensaje = _nota.EsFavorita ? "Nota agregada a favoritas" : "Nota removida de favoritas";
                    await DisplayAlert("Éxito", mensaje, "OK");
                }
                else
                {
                    await DisplayAlert("Error", "No se pudo cambiar el estado de favorita", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error: {ex.Message}", "OK");
            }
        }

        private async void OnEliminarClicked(object sender, EventArgs e)
        {
            try
            {
                bool confirm = await DisplayAlert("Confirmar eliminación",
                    $"¿Está seguro de eliminar esta nota?\n\n" +
                    $"Título: {_nota.Titulo}\n" +
                    $"Fecha: {_nota.FechaTexto}\n" +
                    (_nota.TieneImagen ? "⚠️ La imagen también será eliminada\n" : "") +
                    "\n⚠️ Esta acción no se puede deshacer",
                    "Eliminar", "Cancelar");

                if (confirm)
                {
                    bool resultado = await _viewModel.EliminarNotaAsync(_nota);

                    if (resultado)
                    {
                        await DisplayAlert("Éxito", "Nota eliminada correctamente", "OK");
                        await Navigation.PopAsync(); // Volver a la lista
                    }
                    else
                    {
                        await DisplayAlert("Error", "No se pudo eliminar la nota", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error al eliminar: {ex.Message}", "OK");
            }
        }
    }

    // Página auxiliar para mostrar imagen completa
    public partial class ImagenCompletaPage : ContentPage
    {
        public ImagenCompletaPage(string rutaImagen, NotasViewModel viewModel)
        {
            Title = "Imagen";
            BackgroundColor = Colors.Black;

            var image = new Image
            {
                Aspect = Aspect.AspectFit,
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill
            };

            // Agregar gesto de zoom con PinchGestureRecognizer
            var pinchGesture = new PinchGestureRecognizer();
            double currentScale = 1;
            double startScale = 1;

            pinchGesture.PinchUpdated += (s, e) =>
            {
                if (e.Status == GestureStatus.Started)
                {
                    startScale = currentScale;
                }
                else if (e.Status == GestureStatus.Running)
                {
                    currentScale = startScale * e.Scale;
                    currentScale = Math.Max(0.5, currentScale); // Mínimo 0.5x
                    currentScale = Math.Min(5, currentScale);    // Máximo 5x

                    image.Scale = currentScale;
                }
            };

            // Agregar gesto de doble tap para restablecer zoom
            var tapGesture = new TapGestureRecognizer
            {
                NumberOfTapsRequired = 2
            };

            tapGesture.Tapped += (s, e) =>
            {
                if (currentScale > 1)
                {
                    image.Scale = 1;
                    currentScale = 1;
                }
                else
                {
                    image.Scale = 2;
                    currentScale = 2;
                }
            };

            image.GestureRecognizers.Add(pinchGesture);
            image.GestureRecognizers.Add(tapGesture);

            var scrollView = new ScrollView
            {
                Content = image
            };

            // Cargar imagen
            Task.Run(async () =>
            {
                try
                {
                    var imagenBytes = await viewModel.ObtenerImagenAsync(rutaImagen);
                    if (imagenBytes != null)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            image.Source = ImageSource.FromStream(() => new MemoryStream(imagenBytes));
                        });
                    }
                }
                catch (Exception ex)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlert("Error", $"No se pudo cargar la imagen: {ex.Message}", "OK");
                    });
                }
            });

            Content = scrollView;
        }
    }
}