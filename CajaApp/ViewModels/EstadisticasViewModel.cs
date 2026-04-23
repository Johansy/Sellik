using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CajaApp.Models;
using CajaApp.Services;
using Microsoft.Maui.Controls;

namespace CajaApp.ViewModels
{
    public class EstadisticasViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService _db;
        private bool _isLoading;
        private bool _cargado;
        private DateTime _fechaInicio;
        private DateTime _fechaFin;
        private string _periodoSeleccionado = "Este mes";

        // ── Contadores ──────────────────────────────────────────────────────
        public int TotalCajas { get; private set; }
        public int TotalMovimientos { get; private set; }
        public int TotalVouchers { get; private set; }
        public int TotalNotas { get; private set; }

        // ── Monetarios cajas ────────────────────────────────────────────────
        public decimal TotalDineroContado { get; private set; }
        public decimal PromedioArqueo { get; private set; }
        public decimal ArqueoMaximo { get; private set; }
        public decimal ArqueoMinimo { get; private set; }

        // ── Monetarios movimientos ───────────────────────────────────────────
        public decimal TotalEntradas { get; private set; }
        public decimal TotalSalidas { get; private set; }
        public decimal SaldoMovimientos { get; private set; }

        // ── Vouchers ─────────────────────────────────────────────────────────
        public decimal TotalVouchersImporte { get; private set; }
        public int VouchersCredito { get; private set; }
        public int VouchersDebito { get; private set; }
        public int VouchersEfectivo { get; private set; }
        public int VouchersOtros { get; private set; }

        // ── Ganancia acumulada ────────────────────────────────────────────────
        public decimal UltimoTotalCaja { get; private set; }
        public decimal GananciaAcumulada { get; private set; }

        // ── Notas ────────────────────────────────────────────────────────────
        public int NotasTexto { get; private set; }
        public int NotasImagen { get; private set; }
        public int NotasMixtas { get; private set; }
        public int NotasFavoritas { get; private set; }

        // ── Período ──────────────────────────────────────────────────────────
        public DateTime FechaInicio
        {
            get => _fechaInicio;
            set { _fechaInicio = value; OnPropertyChanged(nameof(FechaInicio)); }
        }

        public DateTime FechaFin
        {
            get => _fechaFin;
            set { _fechaFin = value; OnPropertyChanged(nameof(FechaFin)); }
        }

        public string PeriodoSeleccionado
        {
            get => _periodoSeleccionado;
            set { _periodoSeleccionado = value; OnPropertyChanged(nameof(PeriodoSeleccionado)); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); }
        }

        // ── Textos formateados ───────────────────────────────────────────────
        public string TotalDineroTexto      => $"${TotalDineroContado:F2}";
        public string PromedioArqueoTexto   => TotalCajas > 0 ? $"${PromedioArqueo:F2}" : "—";
        public string ArqueoMaximoTexto     => TotalCajas > 0 ? $"${ArqueoMaximo:F2}" : "—";
        public string ArqueoMinimoTexto     => TotalCajas > 0 ? $"${ArqueoMinimo:F2}" : "—";
        public string TotalEntradasTexto    => $"+${TotalEntradas:F2}";
        public string TotalSalidasTexto     => $"-${TotalSalidas:F2}";
        public string SaldoMovimientosTexto => $"${SaldoMovimientos:F2}";
        public string TotalVouchersTexto    => $"${TotalVouchersImporte:F2}";
        public string UltimoTotalCajaTexto  => TotalCajas > 0 ? $"${UltimoTotalCaja:F2}" : "—";
        public string GananciaAcumuladaTexto => $"${GananciaAcumulada:F2}";
        public string PeriodoTexto          => $"{FechaInicio:dd/MM/yyyy} — {FechaFin:dd/MM/yyyy}";

        // ── ProgressBar (0-1 relativo al máximo entre los 4 módulos) ─────────
        public double ProgressCajas       => _maxActividad > 0 ? (double)TotalCajas / _maxActividad : 0;
        public double ProgressMovimientos => _maxActividad > 0 ? (double)TotalMovimientos / _maxActividad : 0;
        public double ProgressVouchers    => _maxActividad > 0 ? (double)TotalVouchers / _maxActividad : 0;
        public double ProgressNotas       => _maxActividad > 0 ? (double)TotalNotas / _maxActividad : 0;

        private int _maxActividad;

        // ── Lista de opciones de período ─────────────────────────────────────
        public List<string> OpcionesPeriodo { get; } = new()
        {
            "Hoy", "Ayer", "Esta semana", "Esta semana anterior",
            "Este mes", "Mes anterior", "Últimos 7 días",
            "Últimos 30 días", "Últimos 90 días", "Este año", "Todo"
        };

        // ── Actividad más frecuente ──────────────────────────────────────────
        public string ActividadPrincipal { get; private set; } = "—";

        // ── Comandos ─────────────────────────────────────────────────────────
        public ICommand CargarCommand { get; }
        public ICommand CambiarPeriodoCommand { get; }

        public EstadisticasViewModel(DatabaseService db)
        {
            _db = db;

            var hoy = DateTime.Today;
            _fechaInicio = new DateTime(hoy.Year, hoy.Month, 1);
            _fechaFin = hoy;

            CargarCommand = new Command(async () => await CargarAsync());
            CambiarPeriodoCommand = new Command<string>(async (p) => await AplicarPeriodo(p));
        }

        public async Task CargarAsync()
        {
            if (IsLoading) return;
            if (_cargado) return;
            IsLoading = true;
            try
            {
                var cajas       = await _db.ObtenerCajasAsync();
                var movimientos = await _db.ObtenerMovimientosAsync();
                var vouchers    = await _db.ObtenerVouchersAsync();
                var notas       = await _db.ObtenerNotasAsync();

                // Filtrar por período
                var desde = FechaInicio.Date;
                var hasta = FechaFin.Date;

                var cajasFiltradas  = cajas.Where(c => c.Fecha.Date >= desde && c.Fecha.Date <= hasta).ToList();
                var movFiltrados    = movimientos.Where(m => m.Fecha.Date >= desde && m.Fecha.Date <= hasta).ToList();
                var vouFiltrados    = vouchers.Where(v => v.Fecha.Date >= desde && v.Fecha.Date <= hasta).ToList();
                var notasFiltradas  = notas.Where(n => n.FechaCreacion.Date >= desde && n.FechaCreacion.Date <= hasta).ToList();

                // Cajas
                TotalCajas          = cajasFiltradas.Count;
                TotalDineroContado  = cajasFiltradas.Sum(c => c.Total);
                PromedioArqueo      = TotalCajas > 0 ? TotalDineroContado / TotalCajas : 0;
                ArqueoMaximo        = cajasFiltradas.Any() ? cajasFiltradas.Max(c => c.Total) : 0;
                ArqueoMinimo        = cajasFiltradas.Any() ? cajasFiltradas.Min(c => c.Total) : 0;

                // Movimientos
                TotalMovimientos = movFiltrados.Count;
                TotalEntradas    = movFiltrados.Where(m => m.Tipo == TipoMovimiento.Entrada).Sum(m => m.Monto);
                TotalSalidas     = movFiltrados.Where(m => m.Tipo == TipoMovimiento.Salida).Sum(m => m.Monto);
                SaldoMovimientos = TotalEntradas - TotalSalidas;

                // Vouchers
                TotalVouchers        = vouFiltrados.Count;
                TotalVouchersImporte = vouFiltrados.Sum(v => v.Total);
                VouchersCredito      = vouFiltrados.Count(v => v.TipoPago == TipoPago.Credito);
                VouchersDebito       = vouFiltrados.Count(v => v.TipoPago == TipoPago.Debito);
                VouchersEfectivo     = vouFiltrados.Count(v => v.TipoPago == TipoPago.Efectivo);
                VouchersOtros        = vouFiltrados.Count(v => v.TipoPago != TipoPago.Credito &&
                                                               v.TipoPago != TipoPago.Debito &&
                                                               v.TipoPago != TipoPago.Efectivo);

                // Ganancia acumulada
                var ultimaCaja  = cajasFiltradas.OrderByDescending(c => c.Fecha).FirstOrDefault();
                UltimoTotalCaja  = ultimaCaja?.Total ?? 0;
                GananciaAcumulada = UltimoTotalCaja + TotalVouchersImporte - SaldoMovimientos;

                // Notas
                TotalNotas      = notasFiltradas.Count;
                NotasTexto      = notasFiltradas.Count(n => n.Tipo == TipoNota.Texto);
                NotasImagen     = notasFiltradas.Count(n => n.Tipo == TipoNota.Imagen);
                NotasMixtas     = notasFiltradas.Count(n => n.Tipo == TipoNota.TextoConImagen);
                NotasFavoritas  = notasFiltradas.Count(n => n.EsFavorita);

                // Actividad principal
                _maxActividad = Math.Max(Math.Max(TotalCajas, TotalMovimientos), Math.Max(TotalVouchers, TotalNotas));
                ActividadPrincipal = _maxActividad == 0 ? "Sin actividad" :
                    TotalCajas == _maxActividad ? $"💰 Arqueos de caja ({TotalCajas})" :
                    TotalMovimientos == _maxActividad ? $"💸 Movimientos ({TotalMovimientos})" :
                    TotalVouchers == _maxActividad ? $"📄 Vouchers ({TotalVouchers})" :
                    $"📝 Notas ({TotalNotas})";

                NotifyAll();
                _cargado = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EstadisticasViewModel] Error: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task AplicarPeriodo(string periodo)
        {
            _cargado = false;
            var hoy = DateTime.Today;
            PeriodoSeleccionado = periodo;

            switch (periodo)
            {
                case "Hoy":
                    FechaInicio = hoy; FechaFin = hoy; break;
                case "Ayer":
                    FechaInicio = hoy.AddDays(-1); FechaFin = hoy.AddDays(-1); break;
                case "Esta semana":
                    FechaInicio = hoy.AddDays(-(int)hoy.DayOfWeek); FechaFin = hoy; break;
                case "Esta semana anterior":
                    var ini = hoy.AddDays(-(int)hoy.DayOfWeek - 7);
                    FechaInicio = ini; FechaFin = ini.AddDays(6); break;
                case "Este mes":
                    FechaInicio = new DateTime(hoy.Year, hoy.Month, 1); FechaFin = hoy; break;
                case "Mes anterior":
                    var mes = hoy.AddMonths(-1);
                    FechaInicio = new DateTime(mes.Year, mes.Month, 1);
                    FechaFin = new DateTime(mes.Year, mes.Month, DateTime.DaysInMonth(mes.Year, mes.Month)); break;
                case "Últimos 7 días":
                    FechaInicio = hoy.AddDays(-7); FechaFin = hoy; break;
                case "Últimos 30 días":
                    FechaInicio = hoy.AddDays(-30); FechaFin = hoy; break;
                case "Últimos 90 días":
                    FechaInicio = hoy.AddDays(-90); FechaFin = hoy; break;
                case "Este año":
                    FechaInicio = new DateTime(hoy.Year, 1, 1); FechaFin = hoy; break;
                case "Todo":
                    FechaInicio = new DateTime(2020, 1, 1); FechaFin = hoy; break;
            }

            await CargarAsync();
        }

        private void NotifyAll()
        {
            var props = new[]
            {
                nameof(TotalCajas), nameof(TotalMovimientos), nameof(TotalVouchers), nameof(TotalNotas),
                nameof(TotalDineroContado), nameof(PromedioArqueo), nameof(ArqueoMaximo), nameof(ArqueoMinimo),
                nameof(TotalEntradas), nameof(TotalSalidas), nameof(SaldoMovimientos),
                nameof(TotalVouchersImporte), nameof(VouchersCredito), nameof(VouchersDebito),
                nameof(VouchersEfectivo), nameof(VouchersOtros),
                nameof(NotasTexto), nameof(NotasImagen), nameof(NotasMixtas), nameof(NotasFavoritas),
                nameof(TotalDineroTexto), nameof(PromedioArqueoTexto), nameof(ArqueoMaximoTexto), nameof(ArqueoMinimoTexto),
                nameof(TotalEntradasTexto), nameof(TotalSalidasTexto), nameof(SaldoMovimientosTexto), nameof(TotalVouchersTexto),
                nameof(UltimoTotalCaja), nameof(UltimoTotalCajaTexto), nameof(GananciaAcumulada), nameof(GananciaAcumuladaTexto),
                nameof(PeriodoTexto), nameof(ActividadPrincipal),
                nameof(ProgressCajas), nameof(ProgressMovimientos), nameof(ProgressVouchers), nameof(ProgressNotas)
            };
            foreach (var p in props) OnPropertyChanged(p);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
