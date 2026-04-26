using CajaApp.Models;
using System.Text.Json;

namespace CajaApp.Services
{
    public class ConfiguracionService
    {
        private readonly DatabaseService _databaseService;

        private static readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
        private static bool _initialized = false;

        public ConfiguracionService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task InicializarConfiguracionAsync()
        {
            if (_initialized) return;
            await _initLock.WaitAsync();
            try
            {
                if (_initialized) return;
                await InicializarConfiguracionPredeterminada();
                await InicializarDenominacionesPredeterminadas();
                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        private async Task InicializarConfiguracionPredeterminada()
        {
            var configuracionesDefault = new List<ConfiguracionApp>
            {
                new ConfiguracionApp
                {
                    Clave = ConfiguracionApp.Claves.TemaApp,
                    Valor = "2", // Automático por defecto (sigue el tema del sistema)
                    Descripcion = "Tema de la aplicación (0=Claro, 1=Oscuro, 2=Automático)"
                },
                new ConfiguracionApp
                {
                    Clave = ConfiguracionApp.Claves.NombreNegocio,
                    Valor = "Mi Negocio",
                    Descripcion = "Nombre del negocio para reportes"
                },
                new ConfiguracionApp
                {
                    Clave = ConfiguracionApp.Claves.MonedaPrincipal,
                    Valor = "MXN",
                    Descripcion = "Moneda principal (MXN, USD, etc.)"
                },
                new ConfiguracionApp
                {
                    Clave = ConfiguracionApp.Claves.FormatoFecha,
                    Valor = "dd/MM/yyyy",
                    Descripcion = "Formato de fecha para mostrar"
                },
                new ConfiguracionApp
                {
                    Clave = ConfiguracionApp.Claves.AutoGuardado,
                    Valor = "true",
                    Descripcion = "Activar guardado automático"
                },
                new ConfiguracionApp
                {
                    Clave = ConfiguracionApp.Claves.ConfirmacionEliminar,
                    Valor = "true",
                    Descripcion = "Mostrar confirmación al eliminar"
                },
                new ConfiguracionApp
                {
                    Clave = ConfiguracionApp.Claves.TiempoAutoGuardado,
                    Valor = "30",
                    Descripcion = "Segundos entre auto-guardados"
                },
                new ConfiguracionApp
                {
                    Clave = ConfiguracionApp.Claves.MostrarTutorial,
                    Valor = "true",
                    Descripcion = "Mostrar tutorial en primera ejecución"
                },
                new ConfiguracionApp
                {
                    Clave = ConfiguracionApp.Claves.Version,
                    Valor = "1.0.0",
                    Descripcion = "Versión actual de la aplicación"
                },
                new ConfiguracionApp
                {
                    Clave = ConfiguracionApp.Claves.BloquearOrientacion,
                    Valor = "true",
                    Descripcion = "Bloquear la pantalla en modo vertical (portrait)"
                }
            };

            foreach (var config in configuracionesDefault)
            {
                if (!await ExisteConfiguracionAsync(config.Clave))
                {
                    await _databaseService.GuardarConfiguracionAsync(config);
                }
            }
        }

        private async Task InicializarDenominacionesPredeterminadas()
        {
            var denominacionesExistentes = await _databaseService.ObtenerDenominacionesConfigAsync();

            if (!denominacionesExistentes.Any())
            {
                var denominacionesDefault = new List<DenominacionConfig>
                {
                    // Monedas mexicanas
                    new DenominacionConfig { Valor = 0.01m, Simbolo = "1¢", Tipo = TipoDenominacion.Moneda, OrdenVisualizacion = 1, Color = "#8D6E63" },
                    new DenominacionConfig { Valor = 0.05m, Simbolo = "5¢", Tipo = TipoDenominacion.Moneda, OrdenVisualizacion = 2, Color = "#8D6E63" },
                    new DenominacionConfig { Valor = 0.10m, Simbolo = "10¢", Tipo = TipoDenominacion.Moneda, OrdenVisualizacion = 3, Color = "#8D6E63" },
                    new DenominacionConfig { Valor = 0.20m, Simbolo = "20¢", Tipo = TipoDenominacion.Moneda, OrdenVisualizacion = 4, Color = "#8D6E63" },
                    new DenominacionConfig { Valor = 0.50m, Simbolo = "50¢", Tipo = TipoDenominacion.Moneda, OrdenVisualizacion = 5, Color = "#8D6E63" },
                    new DenominacionConfig { Valor = 1.00m, Simbolo = "$1", Tipo = TipoDenominacion.Moneda, OrdenVisualizacion = 6, Color = "#FFB74D" },
                    new DenominacionConfig { Valor = 2.00m, Simbolo = "$2", Tipo = TipoDenominacion.Moneda, OrdenVisualizacion = 7, Color = "#FFB74D" },
                    new DenominacionConfig { Valor = 5.00m, Simbolo = "$5", Tipo = TipoDenominacion.Moneda, OrdenVisualizacion = 8, Color = "#FFB74D" },
                    new DenominacionConfig { Valor = 10.00m, Simbolo = "$10", Tipo = TipoDenominacion.Moneda, OrdenVisualizacion = 9, Color = "#FFB74D" },
                    new DenominacionConfig { Valor = 20.00m, Simbolo = "$20", Tipo = TipoDenominacion.Moneda, OrdenVisualizacion = 10, Color = "#FFB74D" },
                    
                    // Billetes mexicanos
                    new DenominacionConfig { Valor = 20.00m, Simbolo = "$20", Tipo = TipoDenominacion.Billete, OrdenVisualizacion = 11, Color = "#1976D2" },
                    new DenominacionConfig { Valor = 50.00m, Simbolo = "$50", Tipo = TipoDenominacion.Billete, OrdenVisualizacion = 12, Color = "#D32F2F" },
                    new DenominacionConfig { Valor = 100.00m, Simbolo = "$100", Tipo = TipoDenominacion.Billete, OrdenVisualizacion = 13, Color = "#388E3C" },
                    new DenominacionConfig { Valor = 200.00m, Simbolo = "$200", Tipo = TipoDenominacion.Billete, OrdenVisualizacion = 14, Color = "#F57C00" },
                    new DenominacionConfig { Valor = 500.00m, Simbolo = "$500", Tipo = TipoDenominacion.Billete, OrdenVisualizacion = 15, Color = "#7B1FA2" },
                    new DenominacionConfig { Valor = 1000.00m, Simbolo = "$1000", Tipo = TipoDenominacion.Billete, OrdenVisualizacion = 16, Color = "#C2185B" }
                };

                foreach (var denom in denominacionesDefault)
                {
                    await _databaseService.GuardarDenominacionConfigAsync(denom);
                }
            }
        }

        public async Task<string> ObtenerConfiguracionAsync(string clave, string valorDefault = "")
        {
            var config = await _databaseService.ObtenerConfiguracionPorClaveAsync(clave);
            return config?.Valor ?? valorDefault;
        }

        public async Task<bool> GuardarConfiguracionAsync(string clave, string valor, string descripcion = "")
        {
            try
            {
                var config = await _databaseService.ObtenerConfiguracionPorClaveAsync(clave);
                if (config == null)
                {
                    config = new ConfiguracionApp
                    {
                        Clave = clave,
                        Valor = valor,
                        Descripcion = descripcion
                    };
                }
                else
                {
                    config.Valor = valor;
                    config.FechaModificacion = DateTime.Now;
                }

                await _databaseService.GuardarConfiguracionAsync(config);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ExisteConfiguracionAsync(string clave)
        {
            var config = await _databaseService.ObtenerConfiguracionPorClaveAsync(clave);
            return config != null;
        }

        public async Task<TemaAplicacion> ObtenerTemaAsync()
        {
            var tema = await ObtenerConfiguracionAsync(ConfiguracionApp.Claves.TemaApp, "2");
            if (int.TryParse(tema, out int temaInt))
            {
                // Sincronizar Preferences con la BD (fuente de verdad)
                var valorActual = Preferences.Get(ConfiguracionApp.Claves.TemaApp, "2");
                if (valorActual != tema)
                    Preferences.Set(ConfiguracionApp.Claves.TemaApp, tema);
                return (TemaAplicacion)temaInt;
            }
            return TemaAplicacion.Automatico;
        }

        public async Task<bool> CambiarTemaAsync(TemaAplicacion tema)
        {
            var valor = ((int)tema).ToString();
            bool resultado = await GuardarConfiguracionAsync(
                ConfiguracionApp.Claves.TemaApp,
                valor,
                "Tema de la aplicación"
            );
            Preferences.Set(ConfiguracionApp.Claves.TemaApp, valor);
            System.Diagnostics.Debug.WriteLine($"[TEMA_DEBUG] CambiarTemaAsync: tema={tema}, valor={valor}, bdOk={resultado}, prefsAhora={Preferences.Get(ConfiguracionApp.Claves.TemaApp, "?")}");
            return resultado;
        }

        public async Task<List<DenominacionConfig>> ObtenerDenominacionesActivasAsync()
        {
            return await _databaseService.ObtenerDenominacionesActivasAsync();
        }

        public async Task<List<DenominacionConfig>> ObtenerTodasDenominacionesAsync()
        {
            return await _databaseService.ObtenerDenominacionesConfigAsync();
        }

        public async Task<bool> ActualizarEstadoDenominacionAsync(int denominacionId, bool activa)
        {
            try
            {
                var denominacion = await _databaseService.ObtenerDenominacionConfigPorIdAsync(denominacionId);
                if (denominacion != null)
                {
                    denominacion.EstaActiva = activa;
                    denominacion.FechaModificacion = DateTime.Now;
                    await _databaseService.GuardarDenominacionConfigAsync(denominacion);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> AgregarDenominacionPersonalizadaAsync(decimal valor, string simbolo, TipoDenominacion tipo, string color = "#757575")
        {
            try
            {
                var todas = await ObtenerTodasDenominacionesAsync();

                // Calcular el orden correcto: insertar después de todas las denominaciones
                // con valor menor, respetando que Monedas van antes que Billetes del mismo valor.
                int orden = todas
                    .Where(d => d.Valor < valor || (d.Valor == valor && d.Tipo == TipoDenominacion.Moneda && tipo == TipoDenominacion.Billete))
                    .Select(d => d.OrdenVisualizacion)
                    .DefaultIfEmpty(0)
                    .Max() + 1;

                // Desplazar hacia arriba las denominaciones que tenían ese orden o mayor
                foreach (var d in todas.Where(d => d.OrdenVisualizacion >= orden))
                {
                    d.OrdenVisualizacion++;
                    await _databaseService.GuardarDenominacionConfigAsync(d);
                }

                var nuevaDenominacion = new DenominacionConfig
                {
                    Valor = valor,
                    Simbolo = simbolo,
                    Tipo = tipo,
                    Color = color,
                    OrdenVisualizacion = orden,
                    EsPersonalizada = true,
                    EstaActiva = true
                };

                await _databaseService.GuardarDenominacionConfigAsync(nuevaDenominacion);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> EliminarDenominacionPersonalizadaAsync(int denominacionId)
        {
            try
            {
                var denominacion = await _databaseService.ObtenerDenominacionConfigPorIdAsync(denominacionId);
                if (denominacion != null && denominacion.EsPersonalizada)
                {
                    await _databaseService.EliminarDenominacionConfigAsync(denominacion);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<Dictionary<string, string>> ObtenerTodasConfiguracionesAsync()
        {
            var configuraciones = await _databaseService.ObtenerTodasConfiguracionesAsync();
            return configuraciones.ToDictionary(c => c.Clave, c => c.Valor);
        }

        public async Task<bool> RestaurarConfiguracionPredeterminadaAsync()
        {
            try
            {
                // Eliminar configuraciones existentes
                await _databaseService.LimpiarConfiguracionesAsync();

                // Reinicializar con valores por defecto
                await InicializarConfiguracionAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> ExportarConfiguracionAsync()
        {
            try
            {
                var configuraciones = await ObtenerTodasConfiguracionesAsync();
                var denominaciones = await ObtenerTodasDenominacionesAsync();

                var exportData = new
                {
                    FechaExportacion = DateTime.Now,
                    Version = await ObtenerConfiguracionAsync(ConfiguracionApp.Claves.Version),
                    Configuraciones = configuraciones,
                    Denominaciones = denominaciones.Select(d => new
                    {
                        d.Valor,
                        d.Simbolo,
                        d.Tipo,
                        d.EstaActiva,
                        d.OrdenVisualizacion,
                        d.Color,
                        d.EsPersonalizada
                    })
                };

                return JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al exportar configuración: {ex.Message}");
            }
        }
    }
}
