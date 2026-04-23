using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using CajaApp.Models;
using CajaApp.Services;

namespace CajaApp.ViewModels
{
    public enum FiltroMovimiento
    {
        Todo,
        Hoy,
        EstaSemana,
        EsteMes,
        EsteAnio,
        FechaEspecifica
    }

    public class MovimientosViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService _databaseService;
        private readonly SemaphoreSlim _cargarDatosLock;
        private decimal _saldoTotal;
        private decimal _totalEntradas;
        private decimal _totalSalidas;
        private MovimientoEfectivo? _movimientoSeleccionado;
        private bool _isLoading;
        private FiltroMovimiento _filtroActivo = FiltroMovimiento.Todo;
        private DateTime _fechaFiltro = DateTime.Today;

        public ObservableCollection<MovimientoEfectivo> Movimientos { get; set; }
        public ObservableCollection<MovimientoEfectivo> MovimientosFiltrados { get; set; }
        public ObservableCollection<ConceptoMovimiento> ConceptosEntrada { get; set; }
        public ObservableCollection<ConceptoMovimiento> ConceptosSalida { get; set; }

        public FiltroMovimiento FiltroActivo
        {
            get => _filtroActivo;
            set
            {
                _filtroActivo = value;
                OnPropertyChanged(nameof(FiltroActivo));
                AplicarFiltro();
            }
        }

        public DateTime FechaFiltro
        {
            get => _fechaFiltro;
            set
            {
                _fechaFiltro = value;
                OnPropertyChanged(nameof(FechaFiltro));
                if (_filtroActivo == FiltroMovimiento.FechaEspecifica)
                    AplicarFiltro();
            }
        }

        public decimal SaldoTotal
        {
            get => _saldoTotal;
            set
            {
                _saldoTotal = value;
                OnPropertyChanged(nameof(SaldoTotal));
                OnPropertyChanged(nameof(SaldoTexto));
                OnPropertyChanged(nameof(SaldoColor));
            }
        }

        public decimal TotalEntradas
        {
            get => _totalEntradas;
            set
            {
                _totalEntradas = value;
                OnPropertyChanged(nameof(TotalEntradas));
            }
        }

        public decimal TotalSalidas
        {
            get => _totalSalidas;
            set
            {
                _totalSalidas = value;
                OnPropertyChanged(nameof(TotalSalidas));
            }
        }

        public MovimientoEfectivo? MovimientoSeleccionado
        {
            get => _movimientoSeleccionado;
            set
            {
                _movimientoSeleccionado = value;
                OnPropertyChanged(nameof(MovimientoSeleccionado));
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        public string SaldoTexto => $"${SaldoTotal:F2}";
        public string SaldoColor => SaldoTotal >= 0 ? "#4CAF50" : "#F44336";

        public MovimientosViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            _cargarDatosLock = new SemaphoreSlim(1, 1);
            Movimientos = new ObservableCollection<MovimientoEfectivo>();
            MovimientosFiltrados = new ObservableCollection<MovimientoEfectivo>();
            ConceptosEntrada = new ObservableCollection<ConceptoMovimiento>();
            ConceptosSalida = new ObservableCollection<ConceptoMovimiento>();

            _ = InicializarAsync();
        }

        private async Task InicializarAsync()
        {
            try
            {
                await InicializarConceptosPredeterminados();
                await CargarDatos();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MovimientosViewModel] Error en inicialización: {ex}");
            }
        }

        private async Task InicializarConceptosPredeterminados()
        {
            var conceptosExistentes = await _databaseService.ObtenerConceptosAsync();

            if (!conceptosExistentes.Any())
            {
                var conceptosDefault = new List<ConceptoMovimiento>
                {
                    // Entradas
                    new ConceptoMovimiento { Clave = "Concepto_VentasDia",       Nombre = "Ventas del día",    Tipo = TipoMovimiento.Entrada },
                    new ConceptoMovimiento { Clave = "Concepto_PrestamoRecibido", Nombre = "Préstamo recibido", Tipo = TipoMovimiento.Entrada },
                    new ConceptoMovimiento { Clave = "Concepto_Devolucion",       Nombre = "Devolución",        Tipo = TipoMovimiento.Entrada },
                    new ConceptoMovimiento { Clave = "Concepto_IngresoExtra",     Nombre = "Ingreso extra",     Tipo = TipoMovimiento.Entrada },
                    new ConceptoMovimiento { Clave = "Concepto_CapitalInicial",   Nombre = "Capital inicial",   Tipo = TipoMovimiento.Entrada },

                    // Salidas
                    new ConceptoMovimiento { Clave = "Concepto_GastosOperativos", Nombre = "Gastos operativos",  Tipo = TipoMovimiento.Salida },
                    new ConceptoMovimiento { Clave = "Concepto_PagoProveedores",  Nombre = "Pago a proveedores", Tipo = TipoMovimiento.Salida },
                    new ConceptoMovimiento { Clave = "Concepto_PrestamoOtorgado", Nombre = "Préstamo otorgado",  Tipo = TipoMovimiento.Salida },
                    new ConceptoMovimiento { Clave = "Concepto_GastosVarios",     Nombre = "Gastos varios",      Tipo = TipoMovimiento.Salida },
                    new ConceptoMovimiento { Clave = "Concepto_RetiroPersonal",   Nombre = "Retiro personal",    Tipo = TipoMovimiento.Salida }
                };

                foreach (var concepto in conceptosDefault)
                    await _databaseService.GuardarConceptoAsync(concepto);
            }
            else
            {
                // Migrar conceptos existentes que aún no tienen clave asignada
                var mapaClaves = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Ventas del día"]    = "Concepto_VentasDia",
                    ["Préstamo recibido"] = "Concepto_PrestamoRecibido",
                    ["Devolución"]        = "Concepto_Devolucion",
                    ["Ingreso extra"]     = "Concepto_IngresoExtra",
                    ["Capital inicial"]   = "Concepto_CapitalInicial",
                    ["Gastos operativos"] = "Concepto_GastosOperativos",
                    ["Pago a proveedores"]= "Concepto_PagoProveedores",
                    ["Préstamo otorgado"] = "Concepto_PrestamoOtorgado",
                    ["Gastos varios"]     = "Concepto_GastosVarios",
                    ["Retiro personal"]   = "Concepto_RetiroPersonal"
                };

                foreach (var concepto in conceptosExistentes.Where(c => string.IsNullOrEmpty(c.Clave)))
                {
                    if (mapaClaves.TryGetValue(concepto.Nombre, out var clave))
                    {
                        concepto.Clave = clave;
                        await _databaseService.GuardarConceptoAsync(concepto);
                    }
                }
            }

            await CargarConceptos();
        }

        public async Task CargarConceptos()
        {
            var conceptos = await _databaseService.ObtenerConceptosAsync();

            ConceptosEntrada.Clear();
            ConceptosSalida.Clear();

            foreach (var concepto in conceptos.Where(c => c.EsActivo))
            {
                if (concepto.Tipo == TipoMovimiento.Entrada)
                    ConceptosEntrada.Add(concepto);
                else
                    ConceptosSalida.Add(concepto);
            }
        }

        public async Task CargarDatos()
        {
            await _cargarDatosLock.WaitAsync();
            IsLoading = true;

            try
            {
                var movimientos = await _databaseService.ObtenerMovimientosAsync();

                Movimientos.Clear();
                foreach (var mov in movimientos.OrderByDescending(m => m.Fecha))
                {
                    Movimientos.Add(mov);
                }

                AplicarFiltro();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MovimientosViewModel] Error cargando movimientos: {ex}");
                Movimientos.Clear();
                CalcularTotales();
            }
            finally
            {
                IsLoading = false;
                _cargarDatosLock.Release();
            }
        }

        private void CalcularTotales()
        {
            TotalEntradas = MovimientosFiltrados
                .Where(m => m.Tipo == TipoMovimiento.Entrada)
                .Sum(m => m.Monto);

            TotalSalidas = MovimientosFiltrados
                .Where(m => m.Tipo == TipoMovimiento.Salida)
                .Sum(m => m.Monto);

            SaldoTotal = TotalEntradas - TotalSalidas;
        }

        public void AplicarFiltro()
        {
            IEnumerable<MovimientoEfectivo> fuente = Movimientos;

            switch (_filtroActivo)
            {
                case FiltroMovimiento.Hoy:
                    fuente = Movimientos.Where(m => m.Fecha.Date == DateTime.Today);
                    break;
                case FiltroMovimiento.EstaSemana:
                    var inicioSemana = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                    fuente = Movimientos.Where(m => m.Fecha.Date >= inicioSemana && m.Fecha.Date <= inicioSemana.AddDays(6));
                    break;
                case FiltroMovimiento.EsteMes:
                    fuente = Movimientos.Where(m => m.Fecha.Year == DateTime.Today.Year && m.Fecha.Month == DateTime.Today.Month);
                    break;
                case FiltroMovimiento.EsteAnio:
                    fuente = Movimientos.Where(m => m.Fecha.Year == DateTime.Today.Year);
                    break;
                case FiltroMovimiento.FechaEspecifica:
                    fuente = Movimientos.Where(m => m.Fecha.Date == _fechaFiltro.Date);
                    break;
                default:
                    break;
            }

            MovimientosFiltrados.Clear();
            foreach (var mov in fuente)
                MovimientosFiltrados.Add(mov);

            CalcularTotales();
        }

        public List<MovimientoEfectivo> ObtenerMovimientosFiltrados() => MovimientosFiltrados.ToList();

        public async Task<bool> GuardarMovimientoAsync(MovimientoEfectivo movimiento)
        {
            try
            {
                await _databaseService.GuardarMovimientoAsync(movimiento);
                await CargarDatos();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> EliminarMovimientoAsync(MovimientoEfectivo movimiento)
        {
            try
            {
                await _databaseService.EliminarMovimientoAsync(movimiento);
                await CargarDatos();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public string GenerarReporteTexto(DateTime fechaInicio, DateTime fechaFin)
        {
            var movimientosPeriodo = Movimientos
                .Where(m => m.Fecha >= fechaInicio && m.Fecha <= fechaFin)
                .OrderBy(m => m.Fecha)
                .ToList();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== REPORTE DE MOVIMIENTOS ===");
            sb.AppendLine($"Período: {fechaInicio:dd/MM/yyyy} - {fechaFin:dd/MM/yyyy}");
            sb.AppendLine($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}");
            sb.AppendLine();

            var entradas = movimientosPeriodo.Where(m => m.Tipo == TipoMovimiento.Entrada).ToList();
            var salidas = movimientosPeriodo.Where(m => m.Tipo == TipoMovimiento.Salida).ToList();

            sb.AppendLine("ENTRADAS:");
            sb.AppendLine("=========");
            foreach (var entrada in entradas)
            {
                sb.AppendLine($"{entrada.Fecha:dd/MM/yyyy} | +${entrada.Monto:F2} | {entrada.Concepto}");
                if (!string.IsNullOrWhiteSpace(entrada.Descripcion))
                    sb.AppendLine($"   Desc: {entrada.Descripcion}");
            }
            sb.AppendLine($"SUBTOTAL ENTRADAS: +${entradas.Sum(e => e.Monto):F2}");
            sb.AppendLine();

            sb.AppendLine("SALIDAS:");
            sb.AppendLine("========");
            foreach (var salida in salidas)
            {
                sb.AppendLine($"{salida.Fecha:dd/MM/yyyy} | -${salida.Monto:F2} | {salida.Concepto}");
                if (!string.IsNullOrWhiteSpace(salida.Descripcion))
                    sb.AppendLine($"   Desc: {salida.Descripcion}");
            }
            sb.AppendLine($"SUBTOTAL SALIDAS: -${salidas.Sum(s => s.Monto):F2}");
            sb.AppendLine();

            decimal saldoPeriodo = entradas.Sum(e => e.Monto) - salidas.Sum(s => s.Monto);
            sb.AppendLine("==================");
            sb.AppendLine($"SALDO DEL PERÍODO: ${saldoPeriodo:F2}");
            sb.AppendLine($"SALDO TOTAL ACTUAL: ${SaldoTotal:F2}");

            return sb.ToString();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}