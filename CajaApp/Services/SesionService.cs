using CajaApp.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CajaApp.Services
{
    /// Singleton que mantiene la sesión (workspace) actualmente activa.
    /// Los ViewModels consultan aquí el SesionId para filtrar sus datos.
    /// 
    public class SesionService : INotifyPropertyChanged
    {
        private static SesionService? _instance;
        public static SesionService Instance => _instance ??= new SesionService();

        private Sesion? _sesionActual;

        public Sesion? SesionActual
        {
            get => _sesionActual;
            private set
            {
                if (_sesionActual != value)
                {
                    _sesionActual = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HaySesionActiva));
                    OnPropertyChanged(nameof(SesionActualId));
                    OnPropertyChanged(nameof(SesionActualNombre));
                    SesionCambiada?.Invoke(this, value);
                }
            }
        }

        public bool HaySesionActiva => _sesionActual != null;
        public int SesionActualId => _sesionActual?.Id ?? 0;
        public string SesionActualNombre => _sesionActual?.Nombre ?? string.Empty;

        /// Se lanza cada vez que cambia la sesión activa.
        
        public event EventHandler<Sesion?>? SesionCambiada;

        private SesionService() { }

        public void EstablecerSesion(Sesion sesion)
        {
            SesionActual = sesion;
            Preferences.Set("SesionActivaId", sesion.Id);
        }

        public void CerrarSesion()
        {
            SesionActual = null;
            Preferences.Remove("SesionActivaId");
        }

        public int ObtenerSesionGuardadaId() => Preferences.Get("SesionActivaId", 0);

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
