using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using CajaApp.Models;
using CajaApp.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace CajaApp.ViewModels
{
    public class CajaViewModel : INotifyPropertyChanged
    {
        private decimal _total;
        private string _totalTexto = "";
        private readonly DatabaseService _databaseService;
        private readonly ConfiguracionService _configuracionService;

        // Id del registro que se está editando (0 = nuevo)
        public int EditingRegistroId { get; private set; } = 0;

        public ObservableCollection<Denominacion> Denominaciones { get; set; } = new();

        private DateTime _fecha = DateTime.Today;
        public DateTime Fecha
        {
            get => _fecha;
            set
            {
                if (_fecha != value)
                {
                    _fecha = value;
                    OnPropertyChanged(nameof(Fecha));
                }
            }
        }

        private string _nombreNota = string.Empty;
        public string NombreNota
        {
            get => _nombreNota;
            set
            {
                if (_nombreNota != value)
                {
                    _nombreNota = value;
                    OnPropertyChanged(nameof(NombreNota));
                }
            }
        }

        public decimal Total
        {
            get => _total;
            set
            {
                _total = value;
                OnPropertyChanged(nameof(Total));
                TotalTexto = ConvertirNumeroATexto(value);
            }
        }

        public string TotalTexto
        {
            get => _totalTexto;
            set
            {
                _totalTexto = value;
                OnPropertyChanged(nameof(TotalTexto));
            }
        }

        // Para facilitar pruebas UI
        private string _lastSharedText = string.Empty;
        public string LastSharedText
        {
            get => _lastSharedText;
            set
            {
                if (_lastSharedText != value)
                {
                    _lastSharedText = value;
                    OnPropertyChanged(nameof(LastSharedText));
                }
            }
        }

        public ICommand GuardarCajaCommand { get; }
        public ICommand LimpiarCommand { get; }
        public ICommand CompartirCommand { get; }

        // Notifica a la vista si el guardado fue exitoso (true) o falló (false)
        public Action<bool>? OnGuardadoResultado { get; set; }

        /// <summary>Se dispara tras guardar un conteo de caja. El argumento es el registro guardado.</summary>
        public event EventHandler<CajaRegistro>? CajaGuardadaEvent;

        public CajaViewModel(DatabaseService databaseService, ConfiguracionService configuracionService)
        {
            _databaseService = databaseService;
            _configuracionService = configuracionService;

            // Recalcular TotalTexto cuando cambie el idioma
            LocalizationService.Instance.PropertyChanged += (_, _) =>
            {
                TotalTexto = ConvertirNumeroATexto(_total);
            };

            _totalTexto = ConvertirNumeroATexto(0);

            GuardarCajaCommand = new Command(async () =>
            {
                var nombre = string.IsNullOrWhiteSpace(NombreNota) ? "Sin nombre" : NombreNota;
                await GuardarCajaAsync(nombre);
            });

            LimpiarCommand = new Command(() =>
            {
                LimpiarTodo();
                NombreNota = string.Empty;
                Fecha = DateTime.Today;
            });

            CompartirCommand = new Command(async () => await CompartirAsync());

            _ = CargarDenominacionesDesdeConfigAsync();
        }

        private async Task CargarDenominacionesDesdeConfigAsync()
        {
            try
            {
                // Garantiza que las denominaciones predeterminadas existan aunque
                // el usuario nunca haya abierto la página de Configuración.
                await _configuracionService.InicializarConfiguracionAsync();

                var configuradas = await _configuracionService.ObtenerDenominacionesActivasAsync();

                // Preservar cantidades existentes al refrescar
                var cantidadesActuales = Denominaciones
                    .ToDictionary(d => (d.Valor, d.Tipo), d => d.Cantidad);

                foreach (var d in Denominaciones)
                    d.PropertyChanged -= OnDenominacionChanged;

                Denominaciones.Clear();

                foreach (var config in configuradas.OrderBy(c => c.OrdenVisualizacion))
                {
                    var denom = new Denominacion
                    {
                        Valor = config.Valor,
                        Simbolo = config.Simbolo,
                        Color = config.Color,
                        Tipo = config.Tipo,
                        Cantidad = cantidadesActuales.TryGetValue((config.Valor, config.Tipo), out var qty) ? qty : 0
                    };
                    denom.PropertyChanged += OnDenominacionChanged;
                    Denominaciones.Add(denom);
                }

                CalcularTotal();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando denominaciones desde config: {ex.Message}");
            }
        }

        public async Task RefrescarDenominacionesAsync()
        {
            await CargarDenominacionesDesdeConfigAsync();
        }

        private void OnDenominacionChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Denominacion.Cantidad))
            {
                CalcularTotal();
            }
        }

        public void CalcularTotal()
        {
            Total = Denominaciones.Sum(d => d.SubTotal);
        }

        public void LimpiarTodo()
        {
            foreach (var denominacion in Denominaciones)
            {
                denominacion.Cantidad = 0;
            }

            EditingRegistroId = 0;
        }

        public CajaRegistro CrearRegistro(string nombreNota, DateTime fecha)
        {
            return new CajaRegistro
            {
                NombreNota = nombreNota,
                Fecha = fecha,
                Total = Total,
                TotalTexto = TotalTexto,
                Id = EditingRegistroId,
            };
        }

        private IEnumerable<DenominacionValor> CrearDenominacionesValor(IEnumerable<DenominacionConfig> configs)
        {
            return configs
                .Select(c => new DenominacionValor
                {
                    DenominacionConfigId = c.Id,
                    Cantidad = Denominaciones
                        .FirstOrDefault(d => d.Valor == c.Valor && d.Tipo == c.Tipo)?.Cantidad ?? 0
                })
                .Where(dv => dv.Cantidad > 0)
                .ToList();
        }

        public async Task GuardarCajaAsync(string nombreNota)
        {
            var registro = CrearRegistro(nombreNota, Fecha);
            try
            {
                await _databaseService.GuardarCajaAsync(registro);

                // sqlite-net-pcl asigna el Id directamente en el objeto tras InsertAsync
                if (EditingRegistroId == 0)
                    EditingRegistroId = registro.Id;

                // Persistir desglose de denominaciones
                var configs = await _configuracionService.ObtenerDenominacionesActivasAsync();
                var valores = CrearDenominacionesValor(configs);
                await _databaseService.GuardarDenominacionesValorAsync(registro.Id, valores);

                // Notificar al Historial que hay nuevo/actualizado
                CajaGuardadaEvent?.Invoke(this, registro);

                OnGuardadoResultado?.Invoke(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guardando caja: {ex.Message}");
                OnGuardadoResultado?.Invoke(false);
            }
        }

        // Cargar un registro existente y mapear a la colección Denominaciones para editar
        public async Task LoadRegistroAsync(int id)
        {
            try
            {
                var registro = await _databaseService.ObtenerCajaAsync(id);
                if (registro == null)
                    return;

                EditingRegistroId = registro.Id;
                Total = registro.Total;
                TotalTexto = registro.TotalTexto;

                // Cargar cantidades desde DenominacionValores
                var valores = await _databaseService.ObtenerDenominacionesValorAsync(id);
                var configs = await _configuracionService.ObtenerTodasDenominacionesAsync();
                var configPorId = configs.ToDictionary(c => c.Id);

                foreach (var dv in valores)
                {
                    if (!configPorId.TryGetValue(dv.DenominacionConfigId, out var config)) continue;
                    var denom = Denominaciones.FirstOrDefault(d => d.Valor == config.Valor && d.Tipo == config.Tipo);
                    if (denom != null) denom.Cantidad = dv.Cantidad;
                }

                NombreNota = registro.NombreNota ?? string.Empty;
                Fecha = registro.Fecha;

                CalcularTotal();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando registro: {ex.Message}");
            }
        }

        private async Task CompartirAsync()
        {
            try
            {
                var contenido = GenerarTextoParaCompartir();

                // Guardar para pruebas automatizadas
                LastSharedText = contenido;

                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Text = contenido,
                    Title = "Resumen de Caja"
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al compartir: {ex.Message}");
            }
        }

        private string GenerarTextoParaCompartir()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== RESUMEN DE CAJA ===");
            sb.AppendLine($"Nombre: {(!string.IsNullOrWhiteSpace(NombreNota) ? NombreNota : "Sin nombre")}");
            sb.AppendLine($"Fecha: {Fecha:dd/MM/yyyy}");
            sb.AppendLine($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}");
            sb.AppendLine();

            sb.AppendLine("DENOMINACIONES:");
            sb.AppendLine("================");

            foreach (var denom in Denominaciones)
            {
                if (denom.Cantidad > 0)
                {
                    var subtotalTexto = !string.IsNullOrWhiteSpace(denom.SubtotalTexto)
                        ? denom.SubtotalTexto
                        : $"{denom.SubTotal:F2}";
                    sb.AppendLine($"{denom.Simbolo.PadRight(8)} x {denom.Cantidad.ToString().PadLeft(4)} = {subtotalTexto.PadLeft(12)}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("================");
            sb.AppendLine($"TOTAL: ${Total:F2}");
            sb.AppendLine($"EN TEXTO: {TotalTexto}");

            return sb.ToString();
        }

        private string ConvertirNumeroATexto(decimal numero)
        {
            bool esIngles = LocalizationService.Instance.CodigoIdioma == "en";

            if (esIngles)
                return ConvertirNumeroATextoEn(numero);

            if (numero == 0) return "CERO PESOS 00/100 M.N.";

            int parteEntera = (int)numero;
            int centavos = (int)Math.Round((numero - parteEntera) * 100);

            string textoEntero = ConvertirEnteroATexto(parteEntera);
            string palabra = parteEntera == 1 ? "PESO" : "PESOS";

            return $"{textoEntero} {palabra} {centavos:00}/100 M.N.";
        }

        private string ConvertirNumeroATextoEn(decimal numero)
        {
            if (numero == 0) return "ZERO DOLLARS 00/100";

            int parteEntera = (int)numero;
            int centavos = (int)Math.Round((numero - parteEntera) * 100);

            string textoEntero = ConvertirEnteroATextoEn(parteEntera);
            string palabra = parteEntera == 1 ? "DOLLAR" : "DOLLARS";

            return $"{textoEntero} {palabra} {centavos:00}/100";
        }

        private string ConvertirEnteroATextoEn(int numero)
        {
            if (numero == 0) return "ZERO";

            string[] unidades = { "", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE" };
            string[] especiales = { "TEN", "ELEVEN", "TWELVE", "THIRTEEN", "FOURTEEN", "FIFTEEN", "SIXTEEN", "SEVENTEEN", "EIGHTEEN", "NINETEEN" };
            string[] decenas = { "", "", "TWENTY", "THIRTY", "FORTY", "FIFTY", "SIXTY", "SEVENTY", "EIGHTY", "NINETY" };

            if (numero < 10) return unidades[numero];
            if (numero < 20) return especiales[numero - 10];
            if (numero < 100)
            {
                int d = numero / 10;
                int u = numero % 10;
                return decenas[d] + (u > 0 ? "-" + unidades[u] : "");
            }
            if (numero < 1000)
            {
                int c = numero / 100;
                int resto = numero % 100;
                return unidades[c] + " HUNDRED" + (resto > 0 ? " " + ConvertirEnteroATextoEn(resto) : "");
            }
            if (numero < 1000000)
            {
                int miles = numero / 1000;
                int resto = numero % 1000;
                return ConvertirEnteroATextoEn(miles) + " THOUSAND" + (resto > 0 ? " " + ConvertirEnteroATextoEn(resto) : "");
            }

            return numero.ToString();
        }

        private string ConvertirEnteroATexto(int numero)
        {
            if (numero == 0) return "CERO";

            string[] unidades = { "", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE" };
            string[] decenas = { "", "", "VEINTE", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA" };
            string[] especiales = { "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS", "DIECISIETE", "DIECIOCHO", "DIECINUEVE" };
            string[] centenas = { "", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS", "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS" };

            if (numero < 10) return unidades[numero];
            if (numero < 20) return especiales[numero - 10];
            if (numero < 100)
            {
                int d = numero / 10;
                int u = numero % 10;
                return decenas[d] + (u > 0 ? " Y " + unidades[u] : "");
            }
            if (numero < 1000)
            {
                int c = numero / 100;
                int resto = numero % 100;
                string resultado = numero == 100 ? "CIEN" : centenas[c];
                if (resto > 0) resultado += " " + ConvertirEnteroATexto(resto);
                return resultado;
            }
            if (numero < 1000000)
            {
                int miles = numero / 1000;
                int resto = numero % 1000;
                string textoMiles = miles == 1 ? "MIL" : ConvertirEnteroATexto(miles) + " MIL";
                return textoMiles + (resto > 0 ? " " + ConvertirEnteroATexto(resto) : "");
            }

            return numero.ToString();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

