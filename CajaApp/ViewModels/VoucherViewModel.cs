// ViewModels/VoucherViewModel.cs
using System.Collections.ObjectModel;
using System.ComponentModel;
using CajaApp.Models;
using CajaApp.Services;
using CajaApp.Views;

namespace CajaApp.ViewModels
{
    public enum FiltroFechaVoucher
    {
        Todos,
        Hoy,
        EstaSemana,
        EsteMes,
        EsteAnio,
        FechaPersonalizada
    }

    public enum OrdenVoucher
    {
        FechaVoucher,
        FechaEscaneo
    }

    public class VoucherViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService  _databaseService;
        private readonly OCROrchestrator  _orchestrator;
        private readonly ExportService    _exportService;
        private readonly LicenseService   _licenseService;

        private bool     _isProcessing;
        private decimal  _totalVouchers;
        private decimal  _totalCredito;
        private decimal  _totalDebito;
        private string   _textoOCR = string.Empty;
        private string   _modoOCRActual = string.Empty;
        private Voucher? _voucherPrevisualizacion;

        private FiltroFechaVoucher _filtroActivo = FiltroFechaVoucher.Todos;
        private DateTime           _fechaPersonalizada = DateTime.Today;
        private OrdenVoucher       _ordenActivo = OrdenVoucher.FechaVoucher;

        public ObservableCollection<Voucher>          Vouchers         { get; } = new();
        public ObservableCollection<VoucherSeleccion> VouchersConCheck { get; } = new();
        // Todos los vouchers sin filtrar (fuente completa)
        private readonly List<Voucher> _todosLosVouchers = new();

        public FiltroFechaVoucher FiltroActivo
        {
            get => _filtroActivo;
            set { _filtroActivo = value; OnPropertyChanged(nameof(FiltroActivo)); AplicarFiltro(); }
        }

        public DateTime FechaPersonalizada
        {
            get => _fechaPersonalizada;
            set { _fechaPersonalizada = value; OnPropertyChanged(nameof(FechaPersonalizada)); if (_filtroActivo == FiltroFechaVoucher.FechaPersonalizada) AplicarFiltro(); }
        }

        public OrdenVoucher OrdenActivo
        {
            get => _ordenActivo;
            set { _ordenActivo = value; OnPropertyChanged(nameof(OrdenActivo)); AplicarFiltro(); }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set { _isProcessing = value; OnPropertyChanged(nameof(IsProcessing)); }
        }

        public decimal TotalVouchers
        {
            get => _totalVouchers;
            set { _totalVouchers = value; OnPropertyChanged(nameof(TotalVouchers)); }
        }

        public decimal TotalCredito
        {
            get => _totalCredito;
            set { _totalCredito = value; OnPropertyChanged(nameof(TotalCredito)); }
        }

        public decimal TotalDebito
        {
            get => _totalDebito;
            set { _totalDebito = value; OnPropertyChanged(nameof(TotalDebito)); }
        }

        public string TextoOCR
        {
            get => _textoOCR;
            set { _textoOCR = value; OnPropertyChanged(nameof(TextoOCR)); }
        }

        /// <summary>Descripción del motor usado en el último escaneo (se muestra en la UI).</summary>
        public string ModoOCRActual
        {
            get => _modoOCRActual;
            set { _modoOCRActual = value; OnPropertyChanged(nameof(ModoOCRActual)); }
        }

        public Voucher? VoucherPrevisualizacion
        {
            get => _voucherPrevisualizacion;
            set { _voucherPrevisualizacion = value; OnPropertyChanged(nameof(VoucherPrevisualizacion)); }
        }

        /// <summary>Vouchers actualmente visibles (respetan el filtro de fecha).</summary>
        public IEnumerable<Voucher> VouchersFiltrados =>
            Vouchers.AsEnumerable();

        /// <summary>Para exportar: seleccionados si los hay, de lo contrario los filtrados visibles.</summary>
        public IEnumerable<Voucher> VouchersParaExportar
        {
            get
            {
                var seleccionados = VouchersConCheck.Where(v => v.Seleccionado).Select(v => v.Voucher).ToList();
                return seleccionados.Count > 0 ? seleccionados : Vouchers.ToList();
            }
        }

        public VoucherViewModel(DatabaseService databaseService, ExportService exportService, OCROrchestrator orchestrator, LicenseService licenseService)
        {
            _databaseService = databaseService;
            _exportService   = exportService;
            _orchestrator    = orchestrator;
            _licenseService  = licenseService;
            _ = CargarVouchers();
            _ = RefrescarModoOCRAsync();
        }

        // ── OCR ───────────────────────────────────────────────────────────────────

        public async Task<Voucher> ProcesarImagenVoucherAsync(byte[] imagenBytes)
        {
            IsProcessing = true;
            try
            {
                var (voucher, textoOCR) = await _orchestrator.ProcesarAsync(imagenBytes);
                TextoOCR                = textoOCR;
                VoucherPrevisualizacion = voucher;
                ModoOCRActual           = await _orchestrator.ObtenerDescripcionModoAsync();
                return voucher;
            }
            finally { IsProcessing = false; }
        }

        public async Task RefrescarModoOCRAsync()
        {
            ModoOCRActual = await _orchestrator.ObtenerDescripcionModoAsync();
        }

        // ── CRUD ──────────────────────────────────────────────────────────────────

        public async Task CargarVouchers()
        {
            try
            {
                var vouchers = await _databaseService.ObtenerVouchersAsync();
                _todosLosVouchers.Clear();
                _todosLosVouchers.AddRange(vouchers);
                AplicarFiltro();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VoucherVM] Error cargando: {ex.Message}");
            }
        }

        public void AplicarFiltro()
        {
            var hoy = DateTime.Today;
            IEnumerable<Voucher> resultado = _filtroActivo switch
            {
                FiltroFechaVoucher.Hoy =>
                    _todosLosVouchers.Where(v => v.Fecha.Date == hoy),
                FiltroFechaVoucher.EstaSemana =>
                    _todosLosVouchers.Where(v => v.Fecha.Date >= InicioSemana(hoy) && v.Fecha.Date <= hoy),
                FiltroFechaVoucher.EsteMes =>
                    _todosLosVouchers.Where(v => v.Fecha.Year == hoy.Year && v.Fecha.Month == hoy.Month),
                FiltroFechaVoucher.EsteAnio =>
                    _todosLosVouchers.Where(v => v.Fecha.Year == hoy.Year),
                FiltroFechaVoucher.FechaPersonalizada =>
                    _todosLosVouchers.Where(v => v.Fecha.Date == _fechaPersonalizada.Date),
                _ => _todosLosVouchers
            };

            Vouchers.Clear();
            VouchersConCheck.Clear();
            var ordenado = _ordenActivo == OrdenVoucher.FechaEscaneo
                ? resultado.OrderByDescending(v => v.FechaCreacion)
                : resultado.OrderByDescending(v => v.Fecha);
            foreach (var v in ordenado)
            {
                Vouchers.Add(v);
                VouchersConCheck.Add(new VoucherSeleccion(v));
            }
            CalcularTotales();
        }

        private static DateTime InicioSemana(DateTime fecha)
        {
            int diff = (7 + (fecha.DayOfWeek - DayOfWeek.Monday)) % 7;
            return fecha.AddDays(-diff).Date;
        }

        public string ObtenerDescripcionFiltro()
        {
            var hoy = DateTime.Today;
            return _filtroActivo switch
            {
                FiltroFechaVoucher.Hoy              => $"hoy ({hoy:dd/MM/yyyy})",
                FiltroFechaVoucher.EstaSemana       => $"esta semana ({InicioSemana(hoy):dd/MM} – {hoy:dd/MM/yyyy})",
                FiltroFechaVoucher.EsteMes          => $"este mes ({hoy:MMMM yyyy})",
                FiltroFechaVoucher.EsteAnio         => $"este año ({hoy.Year})",
                FiltroFechaVoucher.FechaPersonalizada => $"el {_fechaPersonalizada:dd/MM/yyyy}",
                _                                   => $"todos ({_todosLosVouchers.Count})"
            };
        }

        private void CalcularTotales()
        {
            TotalVouchers = Vouchers.Sum(v => v.Total);
            TotalCredito  = Vouchers.Where(v => v.TipoPago == TipoPago.Credito).Sum(v => v.Total);
            TotalDebito   = Vouchers.Where(v => v.TipoPago == TipoPago.Debito).Sum(v => v.Total);
        }

        public async Task<bool> GuardarVoucherAsync(Voucher voucher)
        {
            try { await _databaseService.GuardarVoucherAsync(voucher); await CargarVouchers(); return true; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[VoucherVM] {ex.Message}"); return false; }
        }

        public async Task<bool> EliminarVoucherAsync(Voucher voucher)
        {
            try { await _databaseService.EliminarVoucherAsync(voucher); await CargarVouchers(); return true; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[VoucherVM] {ex.Message}"); return false; }
        }

        public async Task<bool> EliminarTodosVouchersAsync()
        {
            try { await _databaseService.EliminarTodosVouchersAsync(); await CargarVouchers(); return true; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[VoucherVM] {ex.Message}"); return false; }
        }

        public void LimpiarPrevisualizacion() => VoucherPrevisualizacion = null;

        // ── Selección ─────────────────────────────────────────────────────────────

        public void SeleccionarTodos(bool seleccionar)
        {
            foreach (var v in VouchersConCheck) v.Seleccionado = seleccionar;
        }

        public int ContarSeleccionados() => VouchersConCheck.Count(v => v.Seleccionado);

        // ── Exportación ───────────────────────────────────────────────────────────

        public async Task ExportarExcelAsync(IEnumerable<Voucher>? vouchers = null)
        {
            if (!_licenseService.PuedeExportar())
            {
                await MostrarAlertaPremiumAsync();
                return;
            }
            IsProcessing = true;
            try
            {
                var ruta = _exportService.GenerarExcel((vouchers ?? VouchersParaExportar).ToList());
                await _exportService.CompartirArchivoAsync(ruta, $"Vouchers {DateTime.Now:dd-MM-yyyy}");
            }
            finally { IsProcessing = false; }
        }

        public async Task ExportarPdfAsync(IEnumerable<Voucher>? vouchers = null)
        {
            if (!_licenseService.PuedeExportar())
            {
                await MostrarAlertaPremiumAsync();
                return;
            }
            IsProcessing = true;
            try
            {
                var ruta = _exportService.GenerarPdf((vouchers ?? VouchersParaExportar).ToList());
                await _exportService.CompartirArchivoAsync(ruta, $"Vouchers {DateTime.Now:dd-MM-yyyy}");
            }
            finally { IsProcessing = false; }
        }

        private static async Task MostrarAlertaPremiumAsync()
        {
            var page = Application.Current?.Windows.Count > 0
                ? Application.Current.Windows[0].Page
                : null;
            var irAPremium = await (page?.DisplayAlert(
                LocalizationService.Get("Premium_FuncionTitulo"),
                LocalizationService.Get("Premium_FuncionExportar"),
                LocalizationService.Get("Premium_BtnVerPremium"),
                LocalizationService.Get("Btn_Cancelar")) ?? Task.FromResult(false));
            if (irAPremium)
                await Shell.Current.GoToAsync(nameof(PremiumPage));
        }

        public string GenerarReporteVouchers()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== REPORTE DE VOUCHERS ===");
            sb.AppendLine($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}");
            sb.AppendLine($"Total de vouchers: {Vouchers.Count}");
            sb.AppendLine();
            sb.AppendLine($"  Crédito:  ${TotalCredito:F2}");
            sb.AppendLine($"  Débito:   ${TotalDebito:F2}");
            sb.AppendLine($"  TOTAL:    ${TotalVouchers:F2}");
            sb.AppendLine();
            foreach (var v in Vouchers.OrderByDescending(v => v.Fecha))
            {
                sb.AppendLine($"{v.FechaTexto} | {v.TipoPagoTexto,-14} | ${v.Total:F2}");
                sb.AppendLine($"  Comercio: {v.Comercio}");
                if (!string.IsNullOrEmpty(v.UltimosDigitosTarjeta))
                    sb.AppendLine($"  Tarjeta: ****{v.UltimosDigitosTarjeta}");
                if (!string.IsNullOrWhiteSpace(v.TextoManuscrito))
                    sb.AppendLine($"  ✍️ Manuscrito: {v.TextoManuscrito}");
            }
            return sb.ToString();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string p) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    public class VoucherSeleccion : INotifyPropertyChanged
    {
        private bool _seleccionado;
        public Voucher Voucher       { get; }
        public string  Comercio      => Voucher.Comercio;
        public string  FechaTexto    => Voucher.FechaTexto;
        public string  TipoPagoTexto => Voucher.TipoPagoTexto;
        public string  TotalTexto    => Voucher.TotalTexto;
        public string  ColorTipo     => Voucher.ColorTipo;

        public bool Seleccionado
        {
            get => _seleccionado;
            set { _seleccionado = value; OnPropertyChanged(nameof(Seleccionado)); }
        }

        public VoucherSeleccion(Voucher v) => Voucher = v;
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string p) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
