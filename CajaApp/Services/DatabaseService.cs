using SQLite;
using CajaApp.Models;
using System.Diagnostics;
using System.Linq;

namespace CajaApp.Services
{
    public class DatabaseService
    {
        private sealed class SqliteTableInfo
        {
            public string name { get; set; } = string.Empty;
        }

        private SQLiteAsyncConnection? _database;
        private readonly SesionService _sesionService;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);

        public DatabaseService(SesionService sesionService)
        {
            _sesionService = sesionService;
        }

        public async Task<SQLiteAsyncConnection> GetDatabaseAsync()
        {
            if (_database != null) return _database;

            await _initLock.WaitAsync();
            try
            {
                if (_database != null) return _database;

                string dbPath = Path.Combine(FileSystem.AppDataDirectory, "CajaApp.db3");
                Debug.WriteLine($"[DatabaseService] DB path: {dbPath}");
                Debug.WriteLine($"[DatabaseService] DB exists before open: {File.Exists(dbPath)}");

                var db = new SQLiteAsyncConnection(dbPath);

                try
                {
                    // Crear todas las tablas necesarias
                    await db.CreateTableAsync<Sesion>();
                    await db.CreateTableAsync<CajaRegistro>();
                    await db.CreateTableAsync<MovimientoEfectivo>();
                    await EnsureMovimientoEfectivoSchemaAsync(db);
                    await EnsureSesionIdSchemaAsync(db);
                    await db.CreateTableAsync<ConceptoMovimiento>();
                    await db.CreateTableAsync<Voucher>();
                    await EnsureVoucherSchemaAsync(db);

                    // Tablas usadas por Configuración y Notas
                    await db.CreateTableAsync<ConfiguracionApp>();
                    await db.CreateTableAsync<DenominacionConfig>();
                    await db.CreateTableAsync<Nota>();
                    await db.CreateTableAsync<DenominacionValor>();
                    await MigrarDenominacionesLegadasAsync(db);

                    // Verificar existencia de la tabla "Notas"
                    var existeNotas = await db.ExecuteScalarAsync<int>(
                        "SELECT count(1) FROM sqlite_master WHERE type='table' AND name='Notas'");
                    Debug.WriteLine($"[DatabaseService] Tabla 'Notas' existe: {(existeNotas > 0 ? "SI" : "NO")}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DatabaseService] Error inicializando BD: {ex}");
                    throw;
                }

                _database = db;
            }
            finally
            {
                _initLock.Release();
            }

            return _database;
        }

        private async Task EnsureMovimientoEfectivoSchemaAsync(SQLiteAsyncConnection db)
        {
            try
            {
                var columnas = await db.QueryAsync<SqliteTableInfo>("PRAGMA table_info('MovimientoEfectivo')");
                var nombres = columnas.Select(c => c.name).ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (!nombres.Contains("Descripcion"))
                    await db.ExecuteAsync("ALTER TABLE MovimientoEfectivo ADD COLUMN Descripcion TEXT NOT NULL DEFAULT ''");

                if (!nombres.Contains("Responsable"))
                    await db.ExecuteAsync("ALTER TABLE MovimientoEfectivo ADD COLUMN Responsable TEXT NOT NULL DEFAULT ''");

                if (!nombres.Contains("FechaCreacion"))
                    await db.ExecuteAsync("ALTER TABLE MovimientoEfectivo ADD COLUMN FechaCreacion TEXT NOT NULL DEFAULT '0001-01-01T00:00:00'");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] Error verificando esquema MovimientoEfectivo: {ex}");
            }
        }

        private async Task EnsureSesionIdSchemaAsync(SQLiteAsyncConnection db)
        {
            try
            {
                foreach (var tabla in new[] { "CajaRegistro", "MovimientoEfectivo", "Vouchers", "Notas" })
                {
                    var cols = await db.QueryAsync<SqliteTableInfo>($"PRAGMA table_info('{tabla}')");
                    var nombres = cols.Select(c => c.name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    if (!nombres.Contains("SesionId"))
                        await db.ExecuteAsync($"ALTER TABLE {tabla} ADD COLUMN SesionId INTEGER NOT NULL DEFAULT 0");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] Error verificando esquema SesionId: {ex}");
            }
        }

        private async Task EnsureVoucherSchemaAsync(SQLiteAsyncConnection db)
        {
            try
            {
                var cols = await db.QueryAsync<SqliteTableInfo>("PRAGMA table_info('Vouchers')");
                var nombres = cols.Select(c => c.name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!nombres.Contains("TextoManuscrito"))
                    await db.ExecuteAsync("ALTER TABLE Vouchers ADD COLUMN TextoManuscrito TEXT NOT NULL DEFAULT ''");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] Error verificando esquema Vouchers: {ex}");
            }
        }

        /// Migra las cantidades de denominaciones legadas (columnas fijas en CajaRegistro)
        /// a la tabla DenominacionValores. Se ejecuta una sola vez por BD.
        private async Task MigrarDenominacionesLegadasAsync(SQLiteAsyncConnection db)
        {
            try
            {
                // Comprobar si la tabla legada todavía tiene columnas físicas (Centavos1, etc.)
                var cols = await db.QueryAsync<SqliteTableInfo>("PRAGMA table_info('CajaRegistro')");
                bool tieneColumnasLegadas = cols.Any(c => c.name == "Centavos1");
                if (!tieneColumnasLegadas) return;

                // Solo migrar registros que aún no tengan filas en DenominacionValores
                var registros = await db.QueryAsync<CajaRegistro>("SELECT * FROM CajaRegistro");
                var denominaciones = await db.Table<DenominacionConfig>().ToListAsync();
                var yaExistentes = (await db.Table<DenominacionValor>().ToListAsync())
                    .Select(dv => dv.CajaRegistroId)
                    .ToHashSet();

                // Mapeo valor+tipo -> DenominacionConfigId
                var mapaDenom = denominaciones
                    .ToDictionary(d => (d.Valor, d.Tipo), d => d.Id);

                var (moneda, billete) = (TipoDenominacion.Moneda, TipoDenominacion.Billete);
                var columnasLegadas = new (string col, decimal valor, TipoDenominacion tipo)[]
                {
                    ("Centavos1",   0.01m,   moneda),
                    ("Centavos5",   0.05m,   moneda),
                    ("Centavos10",  0.10m,   moneda),
                    ("Centavos20",  0.20m,   moneda),
                    ("Centavos50",  0.50m,   moneda),
                    ("Peso1",       1.00m,   moneda),
                    ("Peso2",       2.00m,   moneda),
                    ("Peso5",       5.00m,   moneda),
                    ("Peso10",      10.00m,  moneda),
                    ("Peso20",      20.00m,  moneda),
                    ("Billete20",   20.00m,  billete),
                    ("Billete50",   50.00m,  billete),
                    ("Billete100",  100.00m, billete),
                    ("Billete200",  200.00m, billete),
                    ("Billete500",  500.00m, billete),
                    ("Billete1000", 1000.00m, billete),
                };

                foreach (var registro in registros)
                {
                    if (yaExistentes.Contains(registro.Id)) continue;

                    // Leer columnas legadas con una query directa (ya que son [Ignore] en el modelo)
                    var row = (await db.QueryAsync<LegacyDenomRow>(
                        "SELECT Centavos1,Centavos5,Centavos10,Centavos20,Centavos50," +
                        "Peso1,Peso2,Peso5,Peso10,Peso20," +
                        "Billete20,Billete50,Billete100,Billete200,Billete500,Billete1000 " +
                        "FROM CajaRegistro WHERE Id=?", registro.Id))
                        .FirstOrDefault();

                    if (row == null) continue;

                    var cantidades = new int[]
                    {
                        row.Centavos1, row.Centavos5, row.Centavos10, row.Centavos20, row.Centavos50,
                        row.Peso1, row.Peso2, row.Peso5, row.Peso10, row.Peso20,
                        row.Billete20, row.Billete50, row.Billete100, row.Billete200, row.Billete500, row.Billete1000
                    };

                    for (int i = 0; i < columnasLegadas.Length; i++)
                    {
                        if (cantidades[i] == 0) continue;
                        var key = (columnasLegadas[i].valor, columnasLegadas[i].tipo);
                        if (!mapaDenom.TryGetValue(key, out int denomId)) continue;
                        await db.InsertAsync(new DenominacionValor
                        {
                            CajaRegistroId = registro.Id,
                            DenominacionConfigId = denomId,
                            Cantidad = cantidades[i]
                        });
                    }
                }

                Debug.WriteLine("[DatabaseService] Migración de denominaciones legadas completada.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] Error en migración de denominaciones legadas: {ex}");
            }
        }

        // Clase auxiliar solo para leer las columnas legadas de CajaRegistro
        private sealed class LegacyDenomRow
        {
            public int Centavos1 { get; set; }
            public int Centavos5 { get; set; }
            public int Centavos10 { get; set; }
            public int Centavos20 { get; set; }
            public int Centavos50 { get; set; }
            public int Peso1 { get; set; }
            public int Peso2 { get; set; }
            public int Peso5 { get; set; }
            public int Peso10 { get; set; }
            public int Peso20 { get; set; }
            public int Billete20 { get; set; }
            public int Billete50 { get; set; }
            public int Billete100 { get; set; }
            public int Billete200 { get; set; }
            public int Billete500 { get; set; }
            public int Billete1000 { get; set; }
        }

        // Método utilitario para desarrollo: elimina el fichero DB (usar solo en debug)
        public void DeleteDatabaseFileForDebug()
        {
#if DEBUG
            try
            {
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "CajaApp.db3");
                if (_database != null)
                {
                    // cerrar la conexión asíncrona no es directo; nullificamos y luego borramos
                    _database = null;
                }

                if (File.Exists(dbPath))
                {
                    File.Delete(dbPath);
                    Debug.WriteLine($"[DatabaseService] Archivo de BD eliminado: {dbPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] Error borrando archivo BD: {ex}");
            }
#endif
        }

        #region Métodos para Caja Registradora
        public async Task<int> GuardarCajaAsync(CajaRegistro registro)
        {
            var db = await GetDatabaseAsync();

            if (registro.Id == 0)
                registro.SesionId = _sesionService.SesionActualId;

            if (registro.Id != 0)
                return await db.UpdateAsync(registro);
            else
                return await db.InsertAsync(registro);
        }

        public async Task GuardarDenominacionesValorAsync(int cajaRegistroId, IEnumerable<DenominacionValor> valores)
        {
            var db = await GetDatabaseAsync();
            await db.ExecuteAsync("DELETE FROM DenominacionValores WHERE CajaRegistroId = ?", cajaRegistroId);
            foreach (var dv in valores.Where(v => v.Cantidad > 0))
            {
                dv.CajaRegistroId = cajaRegistroId;
                await db.InsertAsync(dv);
            }
        }

        public async Task<List<DenominacionValor>> ObtenerDenominacionesValorAsync(int cajaRegistroId)
        {
            var db = await GetDatabaseAsync();
            return await db.Table<DenominacionValor>()
                           .Where(dv => dv.CajaRegistroId == cajaRegistroId)
                           .ToListAsync();
        }

        public async Task<List<CajaRegistro>> ObtenerCajasAsync()
        {
            var db = await GetDatabaseAsync();
            int sesionId = _sesionService.SesionActualId;
            return await db.Table<CajaRegistro>()
                          .Where(c => c.SesionId == sesionId)
                          .OrderByDescending(c => c.FechaCreacion)
                          .ToListAsync();
        }

        public async Task<int> EliminarCajaAsync(CajaRegistro registro)
        {
            var db = await GetDatabaseAsync();
            return await db.DeleteAsync(registro);
        }

        public async Task<CajaRegistro> ObtenerCajaAsync(int id)
        {
            var db = await GetDatabaseAsync();
            return await db.Table<CajaRegistro>()
                          .Where(c => c.Id == id)
                          .FirstOrDefaultAsync();
        }
        #region Métodos para Configuración
        public async Task<int> GuardarConfiguracionAsync(ConfiguracionApp configuracion)
        {
            var db = await GetDatabaseAsync();

            var existente = await db.Table<ConfiguracionApp>()
                                   .Where(c => c.Clave == configuracion.Clave)
                                   .FirstOrDefaultAsync();

            if (existente != null)
            {
                existente.Valor = configuracion.Valor;
                existente.FechaModificacion = DateTime.Now;
                return await db.UpdateAsync(existente);
            }
            else
            {
                return await db.InsertAsync(configuracion);
            }
        }

        public async Task<ConfiguracionApp> ObtenerConfiguracionPorClaveAsync(string clave)
        {
            var db = await GetDatabaseAsync();
            return await db.Table<ConfiguracionApp>()
                          .Where(c => c.Clave == clave)
                          .FirstOrDefaultAsync();
        }

        public async Task<List<ConfiguracionApp>> ObtenerTodasConfiguracionesAsync()
        {
            var db = await GetDatabaseAsync();
            return await db.Table<ConfiguracionApp>()
                          .OrderBy(c => c.Clave)
                          .ToListAsync();
        }

        public async Task<int> EliminarConfiguracionAsync(ConfiguracionApp configuracion)
        {
            var db = await GetDatabaseAsync();
            return await db.DeleteAsync(configuracion);
        }

        public async Task<bool> LimpiarConfiguracionesAsync()
        {
            try
            {
                var db = await GetDatabaseAsync();
                await db.DeleteAllAsync<ConfiguracionApp>();
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region Métodos para Denominaciones Config
        public async Task<int> GuardarDenominacionConfigAsync(DenominacionConfig denominacion)
        {
            var db = await GetDatabaseAsync();

            if (denominacion.Id != 0)
                return await db.UpdateAsync(denominacion);
            else
                return await db.InsertAsync(denominacion);
        }

        public async Task<List<DenominacionConfig>> ObtenerDenominacionesConfigAsync()
        {
            var db = await GetDatabaseAsync();
            return await db.Table<DenominacionConfig>()
                          .OrderBy(d => d.OrdenVisualizacion)
                          .ToListAsync();
        }

        public async Task<List<DenominacionConfig>> ObtenerDenominacionesActivasAsync()
        {
            var db = await GetDatabaseAsync();
            return await db.Table<DenominacionConfig>()
                          .Where(d => d.EstaActiva)
                          .OrderBy(d => d.OrdenVisualizacion)
                          .ToListAsync();
        }

        public async Task<DenominacionConfig> ObtenerDenominacionConfigPorIdAsync(int id)
        {
            var db = await GetDatabaseAsync();
            return await db.Table<DenominacionConfig>()
                          .Where(d => d.Id == id)
                          .FirstOrDefaultAsync();
        }

        public async Task<int> EliminarDenominacionConfigAsync(DenominacionConfig denominacion)
        {
            var db = await GetDatabaseAsync();
            return await db.DeleteAsync(denominacion);
        }
        #endregion

        #region Métodos para Movimientos de Efectivo
        public async Task<int> GuardarMovimientoAsync(MovimientoEfectivo movimiento)
        {
            var db = await GetDatabaseAsync();

            if (movimiento.Id == 0)
                movimiento.SesionId = _sesionService.SesionActualId;

            if (movimiento.Id != 0)
                return await db.UpdateAsync(movimiento);
            else
                return await db.InsertAsync(movimiento);
        }

        public async Task<List<MovimientoEfectivo>> ObtenerMovimientosAsync()
        {
            var db = await GetDatabaseAsync();
            int sesionId = _sesionService.SesionActualId;
            return await db.Table<MovimientoEfectivo>()
                          .Where(m => m.SesionId == sesionId)
                          .OrderByDescending(m => m.Fecha)
                          .ThenByDescending(m => m.FechaCreacion)
                          .ToListAsync();
        }

        public async Task<int> EliminarMovimientoAsync(MovimientoEfectivo movimiento)
        {
            var db = await GetDatabaseAsync();
            return await db.DeleteAsync(movimiento);
        }

        public async Task<MovimientoEfectivo> ObtenerMovimientoAsync(int id)
        {
            var db = await GetDatabaseAsync();
            return await db.Table<MovimientoEfectivo>()
                          .Where(m => m.Id == id)
                          .FirstOrDefaultAsync();
        }

        public async Task<List<MovimientoEfectivo>> ObtenerMovimientosPorFechaAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var db = await GetDatabaseAsync();
            int sesionId = _sesionService.SesionActualId;
            return await db.Table<MovimientoEfectivo>()
                          .Where(m => m.SesionId == sesionId && m.Fecha >= fechaInicio && m.Fecha <= fechaFin)
                          .OrderBy(m => m.Fecha)
                          .ToListAsync();
        }
        #endregion

        #region Métodos para Conceptos
        public async Task<int> GuardarConceptoAsync(ConceptoMovimiento concepto)
        {
            var db = await GetDatabaseAsync();

            if (concepto.Id != 0)
                return await db.UpdateAsync(concepto);
            else
                return await db.InsertAsync(concepto);
        }

        public async Task<List<ConceptoMovimiento>> ObtenerConceptosAsync()
        {
            var db = await GetDatabaseAsync();
            return await db.Table<ConceptoMovimiento>()
                          .Where(c => c.EsActivo)
                          .OrderBy(c => c.Nombre)
                          .ToListAsync();
        }

        public async Task<int> EliminarConceptoAsync(ConceptoMovimiento concepto)
        {
            var db = await GetDatabaseAsync();
            concepto.EsActivo = false;
            return await db.UpdateAsync(concepto);
        }
        #endregion

        #region Métodos para Vouchers (tercera pestaña)
        public async Task<int> GuardarVoucherAsync(Voucher voucher)
        {
            var db = await GetDatabaseAsync();

            if (voucher.Id == 0)
                voucher.SesionId = _sesionService.SesionActualId;

            if (voucher.Id != 0)
                return await db.UpdateAsync(voucher);
            else
                return await db.InsertAsync(voucher);
        }

        public async Task<List<Voucher>> ObtenerVouchersAsync()
        {
            var db = await GetDatabaseAsync();
            int sesionId = _sesionService.SesionActualId;
            return await db.Table<Voucher>()
                          .Where(v => v.SesionId == sesionId)
                          .OrderByDescending(v => v.FechaCreacion)
                          .ToListAsync();
        }

        public async Task<int> EliminarVoucherAsync(Voucher voucher)
        {
            var db = await GetDatabaseAsync();
            return await db.DeleteAsync(voucher);
        }

        public async Task<int> EliminarTodosVouchersAsync()
        {
            var db = await GetDatabaseAsync();
            return await db.DeleteAllAsync<Voucher>();
        }

        public async Task<Voucher> ObtenerVoucherAsync(int id)
        {
            var db = await GetDatabaseAsync();
            return await db.Table<Voucher>()
                          .Where(v => v.Id == id)
                          .FirstOrDefaultAsync();
        }
        #endregion

        #region Métodos para Notas
        public async Task<int> GuardarNotaAsync(Nota nota)
        {
            var db = await GetDatabaseAsync();

            if (nota.Id == 0)
                nota.SesionId = _sesionService.SesionActualId;

            if (nota.Id != 0)
                return await db.UpdateAsync(nota);
            else
                return await db.InsertAsync(nota);
        }

        public async Task<List<Nota>> ObtenerNotasAsync()
        {
            var db = await GetDatabaseAsync();
            int sesionId = _sesionService.SesionActualId;
            return await db.Table<Nota>()
                          .Where(n => n.SesionId == sesionId)
                          .OrderByDescending(n => n.Fecha)
                          .ToListAsync();
        }

        public async Task<int> EliminarNotaAsync(Nota nota)
        {
            var db = await GetDatabaseAsync();
            return await db.DeleteAsync(nota);
        }

        public async Task<Nota> ObtenerNotaAsync(int id)
        {
            var db = await GetDatabaseAsync();
            return await db.Table<Nota>()
                          .Where(n => n.Id == id)
                          .FirstOrDefaultAsync();
        }
        #endregion

        // Agrega este método a la clase DatabaseService
        public async Task<Dictionary<string, int>> ObtenerEstadisticasAsync()
        {
            var resultado = new Dictionary<string, int>();

            var db = await GetDatabaseAsync();
            int sesionId = _sesionService.SesionActualId;

            // Cajas registradas
            var totalCajas = await db.Table<CajaRegistro>().Where(c => c.SesionId == sesionId).CountAsync();
            resultado["CajasRegistradas"] = totalCajas;

            // Movimientos de efectivo
            var totalMovimientos = await db.Table<MovimientoEfectivo>().Where(m => m.SesionId == sesionId).CountAsync();
            resultado["MovimientosEfectivo"] = totalMovimientos;

            // Vouchers escaneados
            var totalVouchers = await db.Table<Voucher>().Where(v => v.SesionId == sesionId).CountAsync();
            resultado["VouchersEscaneados"] = totalVouchers;

            // Notas creadas
            var totalNotas = await db.Table<Nota>().Where(n => n.SesionId == sesionId).CountAsync();
            resultado["NotasCreadas"] = totalNotas;

            // Notas favoritas
            var totalFavoritas = await db.Table<Nota>().Where(n => n.SesionId == sesionId && n.EsFavorita).CountAsync();
            resultado["NotasFavoritas"] = totalFavoritas;

            // Notas con imagen
            var totalConImagen = await db.Table<Nota>().Where(n => n.SesionId == sesionId && n.TieneImagen).CountAsync();
            resultado["NotasConImagen"] = totalConImagen;

            return resultado;
        }

        #region Métodos para Sesiones
        public async Task<List<Sesion>> ObtenerSesionesAsync()
        {
            var db = await GetDatabaseAsync();
            return await db.Table<Sesion>()
                          .OrderByDescending(s => s.FechaUltimoAcceso)
                          .ToListAsync();
        }

        public async Task<int> GuardarSesionAsync(Sesion sesion)
        {
            var db = await GetDatabaseAsync();
            if (sesion.Id != 0)
                return await db.UpdateAsync(sesion);
            else
                return await db.InsertAsync(sesion);
        }

        public async Task<Sesion?> ObtenerSesionAsync(int id)
        {
            var db = await GetDatabaseAsync();
            return await db.Table<Sesion>().Where(s => s.Id == id).FirstOrDefaultAsync();
        }

        public async Task<int> EliminarSesionAsync(Sesion sesion)
        {
            var db = await GetDatabaseAsync();
            // Borrar todos los datos asociados
            await db.ExecuteAsync("DELETE FROM CajaRegistro WHERE SesionId = ?", sesion.Id);
            await db.ExecuteAsync("DELETE FROM MovimientoEfectivo WHERE SesionId = ?", sesion.Id);
            await db.ExecuteAsync("DELETE FROM Vouchers WHERE SesionId = ?", sesion.Id);
            await db.ExecuteAsync("DELETE FROM Notas WHERE SesionId = ?", sesion.Id);
            return await db.DeleteAsync(sesion);
        }

        /// Conteo total de vouchers (todas las sesiones) — usado por LicenseService.
        public async Task<int> ContarVouchersTotalAsync()
        {
            var db = await GetDatabaseAsync();
            return await db.Table<Voucher>().CountAsync();
        }

        /// Conteo total de sesiones — usado por LicenseService para el límite Free.
        public async Task<int> ContarSesionesAsync()
        {
            var db = await GetDatabaseAsync();
            return await db.Table<Sesion>().CountAsync();
        }
        #endregion
    }
}
#endregion