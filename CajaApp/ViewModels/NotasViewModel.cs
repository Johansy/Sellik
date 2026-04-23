using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using CajaApp.Models;
using CajaApp.Services;

namespace CajaApp.ViewModels
{
    public class NotasViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService _databaseService;
        private readonly ImagenService _imagenService;
        private bool _isLoading;
        private string _filtroTexto = string.Empty;
        private TipoNota? _filtroTipo;
        private bool _soloFavoritas;
        private int _filtroFechaIndice = 0; // 0=Todas, 1=Hoy, 2=Semana, 3=Mes, 4=Año, 5=Específica
        private DateTime? _fechaEspecifica;

        public ObservableCollection<Nota> Notas { get; set; }
        public ObservableCollection<Nota> NotasFiltradas { get; set; }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        public string FiltroTexto
        {
            get => _filtroTexto;
            set
            {
                _filtroTexto = value;
                OnPropertyChanged(nameof(FiltroTexto));
                AplicarFiltros();
            }
        }

        public TipoNota? FiltroTipo
        {
            get => _filtroTipo;
            set
            {
                _filtroTipo = value;
                OnPropertyChanged(nameof(FiltroTipo));
                AplicarFiltros();
            }
        }

        public bool SoloFavoritas
        {
            get => _soloFavoritas;
            set
            {
                _soloFavoritas = value;
                OnPropertyChanged(nameof(SoloFavoritas));
                AplicarFiltros();
            }
        }

        public int FiltroFechaIndice
        {
            get => _filtroFechaIndice;
            set
            {
                _filtroFechaIndice = value;
                OnPropertyChanged(nameof(FiltroFechaIndice));
                AplicarFiltros();
            }
        }

        public DateTime? FechaEspecifica
        {
            get => _fechaEspecifica;
            set
            {
                _fechaEspecifica = value;
                OnPropertyChanged(nameof(FechaEspecifica));
                if (value.HasValue)
                {
                    _filtroFechaIndice = 5;
                    OnPropertyChanged(nameof(FiltroFechaIndice));
                    AplicarFiltros();
                }
            }
        }

        public int TotalNotas => Notas?.Count ?? 0;
        public int NotasConImagen => Notas?.Count(n => n.TieneImagen) ?? 0;
        public int NotasFavoritas => Notas?.Count(n => n.EsFavorita) ?? 0;

        public NotasViewModel(DatabaseService databaseService, ImagenService imagenService)
        {
            _databaseService = databaseService;
            _imagenService = imagenService;
            Notas = new ObservableCollection<Nota>();
            NotasFiltradas = new ObservableCollection<Nota>();

            _ = CargarNotas();
        }

        public async Task CargarNotas()
        {
            IsLoading = true;

            try
            {
                var notas = await _databaseService.ObtenerNotasAsync();

                Notas.Clear();
                foreach (var nota in notas.OrderByDescending(n => n.FechaModificacion))
                {
                    Notas.Add(nota);
                }

                AplicarFiltros();
                ActualizarContadores();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void AplicarFiltros()
        {
            var notasFiltradas = Notas.AsEnumerable();

            // Filtro por texto
            if (!string.IsNullOrWhiteSpace(FiltroTexto))
            {
                notasFiltradas = notasFiltradas.Where(n =>
                    n.Titulo?.Contains(FiltroTexto, StringComparison.OrdinalIgnoreCase) == true ||
                    n.Contenido?.Contains(FiltroTexto, StringComparison.OrdinalIgnoreCase) == true ||
                    n.Etiquetas?.Contains(FiltroTexto, StringComparison.OrdinalIgnoreCase) == true);
            }

            // Filtro por tipo
            if (FiltroTipo.HasValue)
            {
                notasFiltradas = notasFiltradas.Where(n => n.Tipo == FiltroTipo.Value);
            }

            // Filtro solo favoritas
            if (SoloFavoritas)
            {
                notasFiltradas = notasFiltradas.Where(n => n.EsFavorita);
            }

            // Filtro por fecha
            var hoy = DateTime.Today;
            notasFiltradas = _filtroFechaIndice switch
            {
                1 => notasFiltradas.Where(n => n.FechaCreacion.Date == hoy),
                2 => notasFiltradas.Where(n => n.FechaCreacion.Date >= hoy.AddDays(-(int)hoy.DayOfWeek) && n.FechaCreacion.Date <= hoy),
                3 => notasFiltradas.Where(n => n.FechaCreacion.Year == hoy.Year && n.FechaCreacion.Month == hoy.Month),
                4 => notasFiltradas.Where(n => n.FechaCreacion.Year == hoy.Year),
                5 when _fechaEspecifica.HasValue => notasFiltradas.Where(n => n.FechaCreacion.Date == _fechaEspecifica.Value.Date),
                _ => notasFiltradas
            };

            NotasFiltradas.Clear();
            foreach (var nota in notasFiltradas)
            {
                NotasFiltradas.Add(nota);
            }
        }

        private void ActualizarContadores()
        {
            OnPropertyChanged(nameof(TotalNotas));
            OnPropertyChanged(nameof(NotasConImagen));
            OnPropertyChanged(nameof(NotasFavoritas));
        }

        public async Task<bool> GuardarNotaAsync(Nota nota)
        {
            try
            {
                nota.FechaModificacion = DateTime.Now;
                await _databaseService.GuardarNotaAsync(nota);
                await CargarNotas();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> EliminarNotaAsync(Nota nota)
        {
            try
            {
                // Eliminar imagen asociada si existe
                if (!string.IsNullOrEmpty(nota.RutaImagen))
                {
                    _imagenService.EliminarImagen(nota.RutaImagen);
                }

                await _databaseService.EliminarNotaAsync(nota);
                await CargarNotas();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> CambiarFavoritaAsync(Nota nota)
        {
            try
            {
                nota.EsFavorita = !nota.EsFavorita;
                nota.FechaModificacion = DateTime.Now;
                await _databaseService.GuardarNotaAsync(nota);
                await CargarNotas();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GuardarImagenAsync(byte[] imagenBytes, string extension = ".jpg")
        {
            return await _imagenService.GuardarImagenAsync(imagenBytes, extension);
        }

        public async Task<string> GuardarImagenDesdeStreamAsync(Stream stream, string extension = ".jpg")
        {
            return await _imagenService.GuardarImagenDesdeStreamAsync(stream, extension);
        }

        public async Task<byte[]?> ObtenerImagenAsync(string rutaImagen)
        {
            return await _imagenService.ObtenerImagenAsync(rutaImagen);
        }

        public string GenerarReporteNotas()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== REPORTE DE NOTAS ===");
            sb.AppendLine($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}");
            sb.AppendLine($"Total de notas: {TotalNotas}");
            sb.AppendLine($"Notas con imagen: {NotasConImagen}");
            sb.AppendLine($"Notas favoritas: {NotasFavoritas}");
            sb.AppendLine();

            sb.AppendLine("RESUMEN POR TIPO:");
            sb.AppendLine("================");
            var porTipo = Notas.GroupBy(n => n.Tipo);
            foreach (var grupo in porTipo)
            {
                sb.AppendLine($"{grupo.Key}: {grupo.Count()} notas");
            }
            sb.AppendLine();

            sb.AppendLine("LISTADO DE NOTAS:");
            sb.AppendLine("================");

            foreach (var nota in Notas.Take(50)) // Limitar a 50 notas más recientes
            {
                sb.AppendLine($"[{nota.FechaTexto}] {nota.TipoTexto}");
                sb.AppendLine($"Título: {nota.Titulo ?? "Sin título"}");
                if (!string.IsNullOrEmpty(nota.Contenido))
                {
                    var contenido = nota.Contenido.Length > 200
                        ? nota.Contenido.Substring(0, 197) + "..."
                        : nota.Contenido;
                    sb.AppendLine($"Contenido: {contenido}");
                }
                if (nota.EsFavorita)
                    sb.AppendLine("⭐ FAVORITA");
                if (!string.IsNullOrEmpty(nota.Etiquetas))
                    sb.AppendLine($"Etiquetas: {nota.Etiquetas}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public void LimpiarFiltros()
        {
            FiltroTexto = "";
            FiltroTipo = null;
            SoloFavoritas = false;
            _filtroFechaIndice = 0;
            _fechaEspecifica = null;
            OnPropertyChanged(nameof(FiltroFechaIndice));
            OnPropertyChanged(nameof(FechaEspecifica));
            AplicarFiltros();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}