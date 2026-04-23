using System.Collections.ObjectModel;
using System.ComponentModel;
using CajaApp.Models;
using CajaApp.Services;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace CajaApp.ViewModels
{
    public class HistorialViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService _databaseService;
        private bool _isLoading;
        private DateTime _fechaInicio;
        private DateTime _fechaFin;
        private string _filtroTexto = string.Empty;
        private string _filtroTipo = "Todos";
        private bool _ordenDescendente = true;
        private readonly List<HistorialItem> _todosLosElementos = new();

        public ObservableCollection<CajaRegistro> CajasRegistradas { get; set; }
        public ObservableCollection<MovimientoEfectivo> MovimientosEfectivo { get; set; }
        public ObservableCollection<Voucher> VouchersEscaneados { get; set; }
        public ObservableCollection<Nota> NotasCreadas { get; set; }

        public ObservableCollection<HistorialGroup> ElementosAgrupados { get; set; }
        public ObservableCollection<EstadisticaItem> Estadisticas { get; set; }
        public List<string> TiposFiltro { get; } = new() { "Todos", "Cajas", "Movimientos", "Vouchers", "Notas" };

        public ICommand ItemSelectedCommand { get; }
        public ICommand ToggleOrdenCommand { get; }

        private bool _datosCargados;
        public bool DatosCargados => _datosCargados;

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        public DateTime FechaInicio
        {
            get => _fechaInicio;
            set
            {
                _fechaInicio = value;
                OnPropertyChanged(nameof(FechaInicio));
                if (_datosCargados) _ = CargarDatos();
            }
        }

        public DateTime FechaFin
        {
            get => _fechaFin;
            set
            {
                _fechaFin = value;
                OnPropertyChanged(nameof(FechaFin));
                if (_datosCargados) _ = CargarDatos();
            }
        }

        public string FiltroTexto
        {
            get => _filtroTexto;
            set
            {
                _filtroTexto = value;
                OnPropertyChanged(nameof(FiltroTexto));
                ActualizarAgrupados();
            }
        }

        public string FiltroTipo
        {
            get => _filtroTipo;
            set
            {
                _filtroTipo = value ?? "Todos";
                OnPropertyChanged(nameof(FiltroTipo));
                ActualizarAgrupados();
            }
        }

        public bool OrdenDescendente
        {
            get => _ordenDescendente;
            set
            {
                _ordenDescendente = value;
                OnPropertyChanged(nameof(OrdenDescendente));
                OnPropertyChanged(nameof(IconoOrden));
                ActualizarAgrupados();
            }
        }

        public string IconoOrden => _ordenDescendente ? "↓" : "↑";

        // Estadísticas generales
        public int TotalCajas { get; private set; }
        public int TotalMovimientos { get; private set; }
        public int TotalVouchers { get; private set; }
        public int TotalNotas { get; private set; }
        public decimal TotalDineroContado { get; private set; }
        public decimal SaldoMovimientos { get; private set; }
        public decimal TotalVouchersImporte { get; private set; }

        public HistorialViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            CajasRegistradas = new ObservableCollection<CajaRegistro>();
            MovimientosEfectivo = new ObservableCollection<MovimientoEfectivo>();
            VouchersEscaneados = new ObservableCollection<Voucher>();
            NotasCreadas = new ObservableCollection<Nota>();
            ElementosAgrupados = new ObservableCollection<HistorialGroup>();
            Estadisticas = new ObservableCollection<EstadisticaItem>();

            // Comando para manejar selección en la vista (CollectionView.SelectionChanged)
            ItemSelectedCommand = new Command<SelectionChangedEventArgs>(async (args) =>
            {
                var seleccionado = args?.CurrentSelection?.FirstOrDefault() as HistorialItem;
                if (seleccionado != null)
                    await OnItemSelected(seleccionado);
            });

            ToggleOrdenCommand = new Command(() => OrdenDescendente = !OrdenDescendente);

            // Configurar fechas por defecto (último mes)
            _fechaFin = DateTime.Today;
            _fechaInicio = DateTime.Today.AddDays(-30);
        }

        private async Task InicializarAsync()
        {
            await CargarDatos();
            await GenerarEstadisticas();
        }

        public async Task CargarDatos()
        {
            IsLoading = true;

            try
            {
                // Cargar datos de todas las tablas
                var cajas = await _databaseService.ObtenerCajasAsync();
                var movimientos = await _databaseService.ObtenerMovimientosAsync();
                var vouchers = await _databaseService.ObtenerVouchersAsync();
                var notas = await _databaseService.ObtenerNotasAsync();

                // Filtrar por rango de fechas
                var cajasFiltradas = cajas.Where(c => c.Fecha.Date >= FechaInicio.Date && c.Fecha.Date <= FechaFin.Date);
                var movimientosFiltrados = movimientos.Where(m => m.Fecha.Date >= FechaInicio.Date && m.Fecha.Date <= FechaFin.Date);
                var vouchersFiltrados = vouchers.Where(v => v.Fecha.Date >= FechaInicio.Date && v.Fecha.Date <= FechaFin.Date);
                var notasFiltradas = notas.Where(n => n.Fecha.Date >= FechaInicio.Date && n.Fecha.Date <= FechaFin.Date);

                // Actualizar colecciones
                CajasRegistradas.Clear();
                foreach (var caja in cajasFiltradas.OrderByDescending(c => c.FechaCreacion))
                    CajasRegistradas.Add(caja);

                MovimientosEfectivo.Clear();
                foreach (var movimiento in movimientosFiltrados.OrderByDescending(m => m.Fecha))
                    MovimientosEfectivo.Add(movimiento);

                VouchersEscaneados.Clear();
                foreach (var voucher in vouchersFiltrados.OrderByDescending(v => v.Fecha))
                    VouchersEscaneados.Add(voucher);

                NotasCreadas.Clear();
                foreach (var nota in notasFiltradas.OrderByDescending(n => n.FechaModificacion))
                    NotasCreadas.Add(nota);

                // Crear historial unificado ordenado por fecha
                ActualizarHistorialUnificado();
                CalcularTotales();
                ActualizarContadores();
                _datosCargados = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ActualizarHistorialUnificado()
        {
            _todosLosElementos.Clear();

            // Agregar cajas
            foreach (var caja in CajasRegistradas)
            {
                _todosLosElementos.Add(new HistorialItem
                {
                    Fecha = caja.FechaCreacion,
                    Tipo = "Caja",
                    Categoria = "Cajas",
                    Titulo = caja.NombreNota ?? "Caja sin nombre",
                    Descripcion = $"Total: ${caja.Total:F2}",
                    Icono = "💰",
                    Color = "#2E7D32",
                    ObjetoOriginal = caja
                });
            }

            // Agregar movimientos
            foreach (var movimiento in MovimientosEfectivo)
            {
                _todosLosElementos.Add(new HistorialItem
                {
                    Fecha = movimiento.FechaCreacion,
                    Tipo = movimiento.TipoTexto,
                    Categoria = "Movimientos",
                    Titulo = movimiento.Concepto,
                    Descripcion = $"{movimiento.MontoTexto} - {movimiento.Descripcion}",
                    Icono = movimiento.Tipo == TipoMovimiento.Entrada ? "📈" : "📉",
                    Color = movimiento.Tipo == TipoMovimiento.Entrada ? "#4CAF50" : "#F44336",
                    ObjetoOriginal = movimiento
                });
            }

            // Agregar vouchers
            foreach (var voucher in VouchersEscaneados)
            {
                _todosLosElementos.Add(new HistorialItem
                {
                    Fecha = voucher.FechaCreacion,
                    Tipo = $"Voucher {voucher.TipoPagoTexto}",
                    Categoria = "Vouchers",
                    Titulo = voucher.Comercio,
                    Descripcion = $"{voucher.TotalTexto} - {voucher.UltimosDigitosTarjeta}",
                    Icono = "📄",
                    Color = voucher.ColorTipo,
                    ObjetoOriginal = voucher
                });
            }

            // Agregar notas
            foreach (var nota in NotasCreadas)
            {
                _todosLosElementos.Add(new HistorialItem
                {
                    Fecha = nota.FechaCreacion,
                    Tipo = nota.TipoTexto,
                    Categoria = "Notas",
                    Titulo = nota.Titulo ?? "Nota sin título",
                    Descripcion = nota.ResumenContenido,
                    Icono = nota.EsFavorita ? "⭐" : "📝",
                    Color = nota.ColorTipo,
                    ObjetoOriginal = nota
                });
            }

            ActualizarAgrupados();
        }

        // Al seleccionar un elemento del historial
        private async Task OnItemSelected(HistorialItem item)
        {
            if (item == null) return;

            // Si es un conteo (Caja), navegar a la pestaña de conteo y notificar para cargar el registro
            if (item.ObjetoOriginal is CajaRegistro caja)
            {
                // 1) Navegar a la pestaña/página de conteo
                // Ajusta la ruta abajo a la que uses en tu Shell para la pestaña de conteo.
                // Ejemplo: await Shell.Current.GoToAsync("//main/caja"); 
                await Shell.Current.GoToAsync("//Caja"); // <-- REEMPLAZA con tu ruta real

                // 2) Enviar mensaje para que el CajaViewModel cargue el registro
                // Importante: enviar después de la navegación para asegurar que el VM esté suscrito.
#pragma warning disable CS0618
                MessagingCenter.Send(this, "EditarCaja", caja.Id);
#pragma warning restore CS0618
            }
            else
            {
                // manejar otros tipos si se desea (vouchers, notas, movimientos)
            }
        }

        private void AplicarFiltros()
        {
            ActualizarAgrupados();
        }

        private void ActualizarAgrupados()
        {
            var texto = FiltroTexto?.Trim() ?? string.Empty;

            IEnumerable<HistorialItem> fuente = _todosLosElementos;

            // Filtro por tipo
            if (FiltroTipo != "Todos")
                fuente = fuente.Where(h => h.Categoria == FiltroTipo);

            // Filtro por texto
            if (!string.IsNullOrWhiteSpace(texto))
                fuente = fuente.Where(h =>
                    h.Titulo.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                    h.Descripcion.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                    h.Tipo.Contains(texto, StringComparison.OrdinalIgnoreCase));

            // Orden
            fuente = OrdenDescendente
                ? fuente.OrderByDescending(h => h.Fecha)
                : fuente.OrderBy(h => h.Fecha);

            var lista = fuente.ToList();

            // Actualizar colección agrupada
            ElementosAgrupados.Clear();
            var categorias = new[]
            {
                ("Cajas", "💰"),
                ("Movimientos", "💸"),
                ("Vouchers", "📄"),
                ("Notas", "📝")
            };
            foreach (var (cat, icono) in categorias)
            {
                var items = lista.Where(h => h.Categoria == cat).ToList();
                if (items.Count > 0)
                    ElementosAgrupados.Add(new HistorialGroup(cat, icono, items));
            }
        }

        private void CalcularTotales()
        {
            TotalDineroContado = CajasRegistradas.Sum(c => c.Total);

            var entradas = MovimientosEfectivo.Where(m => m.Tipo == TipoMovimiento.Entrada).Sum(m => m.Monto);
            var salidas = MovimientosEfectivo.Where(m => m.Tipo == TipoMovimiento.Salida).Sum(m => m.Monto);
            SaldoMovimientos = entradas - salidas;

            TotalVouchersImporte = VouchersEscaneados.Sum(v => v.Total);
        }

        private void ActualizarContadores()
        {
            TotalCajas = CajasRegistradas.Count;
            TotalMovimientos = MovimientosEfectivo.Count;
            TotalVouchers = VouchersEscaneados.Count;
            TotalNotas = NotasCreadas.Count;

            OnPropertyChanged(nameof(TotalCajas));
            OnPropertyChanged(nameof(TotalMovimientos));
            OnPropertyChanged(nameof(TotalVouchers));
            OnPropertyChanged(nameof(TotalNotas));
            OnPropertyChanged(nameof(TotalDineroContado));
            OnPropertyChanged(nameof(SaldoMovimientos));
            OnPropertyChanged(nameof(TotalVouchersImporte));
        }

        public async Task GenerarEstadisticas()
        {
            try
            {
                var stats = await _databaseService.ObtenerEstadisticasAsync();

                Estadisticas.Clear();
                Estadisticas.Add(new EstadisticaItem { Titulo = "Cajas Registradas", Valor = stats.GetValueOrDefault("CajasRegistradas", 0), Icono = "💰", Color = "#2E7D32" });
                Estadisticas.Add(new EstadisticaItem { Titulo = "Movimientos", Valor = stats.GetValueOrDefault("MovimientosEfectivo", 0), Icono = "💸", Color = "#FF9800" });
                Estadisticas.Add(new EstadisticaItem { Titulo = "Vouchers", Valor = stats.GetValueOrDefault("VouchersEscaneados", 0), Icono = "📄", Color = "#673AB7" });
                Estadisticas.Add(new EstadisticaItem { Titulo = "Notas", Valor = stats.GetValueOrDefault("NotasCreadas", 0), Icono = "📝", Color = "#9C27B0" });
                Estadisticas.Add(new EstadisticaItem { Titulo = "Favoritas", Valor = stats.GetValueOrDefault("NotasFavoritas", 0), Icono = "⭐", Color = "#FFD700" });
                Estadisticas.Add(new EstadisticaItem { Titulo = "Con Imagen", Valor = stats.GetValueOrDefault("NotasConImagen", 0), Icono = "📷", Color = "#4CAF50" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error generando estadísticas: {ex.Message}");
            }
        }

        public void EstablecerPeriodo(PeriodoTiempo periodo)
        {
            var hoy = DateTime.Today;

            switch (periodo)
            {
                case PeriodoTiempo.Hoy:
                    FechaInicio = hoy;
                    FechaFin = hoy;
                    break;
                case PeriodoTiempo.Ayer:
                    FechaInicio = hoy.AddDays(-1);
                    FechaFin = hoy.AddDays(-1);
                    break;
                case PeriodoTiempo.EstaSemana:
                    FechaInicio = hoy.AddDays(-(int)hoy.DayOfWeek);
                    FechaFin = hoy;
                    break;
                case PeriodoTiempo.SemanaAnterior:
                    var inicioSemanaAnterior = hoy.AddDays(-(int)hoy.DayOfWeek - 7);
                    FechaInicio = inicioSemanaAnterior;
                    FechaFin = inicioSemanaAnterior.AddDays(6);
                    break;
                case PeriodoTiempo.EsteMes:
                    FechaInicio = new DateTime(hoy.Year, hoy.Month, 1);
                    FechaFin = hoy;
                    break;
                case PeriodoTiempo.MesAnterior:
                    var mesAnterior = hoy.AddMonths(-1);
                    FechaInicio = new DateTime(mesAnterior.Year, mesAnterior.Month, 1);
                    FechaFin = new DateTime(mesAnterior.Year, mesAnterior.Month, DateTime.DaysInMonth(mesAnterior.Year, mesAnterior.Month));
                    break;
                case PeriodoTiempo.Ultimos7Dias:
                    FechaInicio = hoy.AddDays(-7);
                    FechaFin = hoy;
                    break;
                case PeriodoTiempo.Ultimos30Dias:
                    FechaInicio = hoy.AddDays(-30);
                    FechaFin = hoy;
                    break;
                case PeriodoTiempo.Ultimos90Dias:
                    FechaInicio = hoy.AddDays(-90);
                    FechaFin = hoy;
                    break;
                case PeriodoTiempo.EsteAno:
                    FechaInicio = new DateTime(hoy.Year, 1, 1);
                    FechaFin = hoy;
                    break;
            }
        }

        public string GenerarReporteCompleto()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== REPORTE HISTORIAL COMPLETO ===");
            sb.AppendLine($"Período: {FechaInicio:dd/MM/yyyy} - {FechaFin:dd/MM/yyyy}");
            sb.AppendLine($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}");
            sb.AppendLine();

            sb.AppendLine("RESUMEN EJECUTIVO:");
            sb.AppendLine("=================");
            sb.AppendLine($"Total de cajas registradas: {TotalCajas}");
            sb.AppendLine($"Total dinero contado: ${TotalDineroContado:F2}");
            sb.AppendLine($"Total movimientos de efectivo: {TotalMovimientos}");
            sb.AppendLine($"Saldo de movimientos: ${SaldoMovimientos:F2}");
            sb.AppendLine($"Total vouchers procesados: {TotalVouchers}");
            sb.AppendLine($"Importe total vouchers: ${TotalVouchersImporte:F2}");
            sb.AppendLine($"Total notas creadas: {TotalNotas}");
            sb.AppendLine();

            if (CajasRegistradas.Any())
            {
                sb.AppendLine("CAJAS REGISTRADAS:");
                sb.AppendLine("=================");
                foreach (var caja in CajasRegistradas.Take(20))
                {
                    sb.AppendLine($"{caja.Fecha:dd/MM/yyyy} - {caja.NombreNota ?? "Sin nombre"} - ${caja.Total:F2}");
                }
                sb.AppendLine();
            }

            if (MovimientosEfectivo.Any())
            {
                sb.AppendLine("MOVIMIENTOS DE EFECTIVO:");
                sb.AppendLine("=======================");
                foreach (var mov in MovimientosEfectivo.Take(20))
                {
                    sb.AppendLine($"{mov.Fecha:dd/MM/yyyy} - {mov.TipoTexto} - {mov.Concepto} - {mov.MontoTexto}");
                }
                sb.AppendLine();
            }

            if (VouchersEscaneados.Any())
            {
                sb.AppendLine("VOUCHERS PROCESADOS:");
                sb.AppendLine("===================");
                foreach (var voucher in VouchersEscaneados.Take(20))
                {
                    sb.AppendLine($"{voucher.Fecha:dd/MM/yyyy} - {voucher.Comercio} - {voucher.TipoPagoTexto} - {voucher.TotalTexto}");
                }
                sb.AppendLine();
            }

            if (NotasCreadas.Any())
            {
                sb.AppendLine("NOTAS CREADAS:");
                sb.AppendLine("=============");
                foreach (var nota in NotasCreadas.Take(20))
                {
                    sb.AppendLine($"{nota.Fecha:dd/MM/yyyy} - {nota.TipoTexto} - {nota.Titulo ?? "Sin título"}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Clases auxiliares
    public class HistorialItem
    {
        public DateTime Fecha { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Icono { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public object? ObjetoOriginal { get; set; }

        public string FechaTexto => Fecha.ToString("dd/MM/yyyy HH:mm");
        public string FechaCorta => Fecha.ToString("dd/MM");
    }

    public class HistorialGroup : List<HistorialItem>
    {
        public string Nombre { get; }
        public string Icono { get; }
        public int Cantidad => Count;
        public HistorialGroup(string nombre, string icono, IEnumerable<HistorialItem> items)
            : base(items)
        {
            Nombre = nombre;
            Icono = icono;
        }
    }

    public class EstadisticaItem
    {
        public string Titulo { get; set; } = string.Empty;
        public int Valor { get; set; }
        public string Icono { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string ValorTexto => Valor.ToString("N0");
    }

    public enum PeriodoTiempo
    {
        Hoy,
        Ayer,
        EstaSemana,
        SemanaAnterior,
        EsteMes,
        MesAnterior,
        Ultimos7Dias,
        Ultimos30Dias,
        Ultimos90Dias,
        EsteAno
    }
}

