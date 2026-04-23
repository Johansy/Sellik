using CajaApp.Models;
using CajaApp.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CajaApp.ViewModels
{
    public class SesionesViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService _db;

        public ObservableCollection<Sesion> Sesiones { get; } = new();

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        private string _nuevaSesionNombre = string.Empty;
        public string NuevaSesionNombre
        {
            get => _nuevaSesionNombre;
            set { _nuevaSesionNombre = value; OnPropertyChanged(); }
        }

        private string _nuevaSesionDescripcion = string.Empty;
        public string NuevaSesionDescripcion
        {
            get => _nuevaSesionDescripcion;
            set { _nuevaSesionDescripcion = value; OnPropertyChanged(); }
        }

        public ICommand CargarSesionesCommand { get; }
        public ICommand CrearSesionCommand { get; }
        public ICommand EliminarSesionCommand { get; }
        public ICommand AbrirSesionCommand { get; }

        public event Action<Sesion>? SesionSeleccionada;

        public SesionesViewModel(DatabaseService db)
        {
            _db = db;

            CargarSesionesCommand = new Command(async () => await CargarSesionesAsync());
            CrearSesionCommand = new Command(async () => await CrearSesionAsync());
            EliminarSesionCommand = new Command<Sesion>(async (s) => await EliminarSesionAsync(s));
            AbrirSesionCommand = new Command<Sesion>(AbrirSesion, sesion => sesion != null);
        }

        public async Task CargarSesionesAsync()
        {
            IsBusy = true;
            try
            {
                var lista = await _db.ObtenerSesionesAsync();
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Sesiones.Clear();
                    foreach (var s in lista)
                        Sesiones.Add(s);
                });
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task CrearSesionAsync()
        {
            if (string.IsNullOrWhiteSpace(NuevaSesionNombre))
                return;

            IsBusy = true;
            try
            {
                var sesion = new Sesion
                {
                    Nombre = NuevaSesionNombre.Trim(),
                    Descripcion = NuevaSesionDescripcion.Trim(),
                    FechaCreacion = DateTime.Now,
                    FechaUltimoAcceso = DateTime.Now
                };

                await _db.GuardarSesionAsync(sesion);
                NuevaSesionNombre = string.Empty;
                NuevaSesionDescripcion = string.Empty;
                await CargarSesionesAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task EliminarSesionAsync(Sesion sesion)
        {
            try
            {
                if (SesionService.Instance.SesionActualId == sesion.Id)
                    SesionService.Instance.CerrarSesion();

                await _db.EliminarSesionAsync(sesion);
                await CargarSesionesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SesionesViewModel] Error eliminando sesión: {ex}");
            }
        }

        private void AbrirSesion(Sesion sesion)
        {
            if (sesion is null)
                return;

            sesion.FechaUltimoAcceso = DateTime.Now;
            _ = _db.GuardarSesionAsync(sesion);
            SesionService.Instance.EstablecerSesion(sesion);
            SesionSeleccionada?.Invoke(sesion);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
