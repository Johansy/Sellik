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

        public async Task<SQLiteAsyncConnection> GetDatabaseAsync()
        {
            if (_database == null)
            {
                string dbPath = Path.Combine(FileSystem.AppDataDirectory, "CajaApp.db3");
                Debug.WriteLine($"[DatabaseService] DB path: {dbPath}");
                Debug.WriteLine($"[DatabaseService] DB exists before open: {File.Exists(dbPath)}");

                _database = new SQLiteAsyncConnection(dbPath);

                try
                {
                    // Crear todas las tablas necesarias
                    await _database.CreateTableAsync<Sesion>();
                    await _database.CreateTableAsync<CajaRegistro>();
                    await _database.CreateTableAsync<MovimientoEfectivo>();
                    await EnsureMovimientoEfectivoSchemaAsync(_database);
                    await EnsureSesionIdSchemaAsync(_database);
                    await _database.CreateTableAsync<ConceptoMovimiento>();
                    await _database.CreateTableAsync<Voucher>();
                        await EnsureVoucherSchemaAsync(_database);

                        // Tablas usadas por Configuración y Notas
                    await _database.CreateTableAsync<ConfiguracionApp>();
                    await _database.CreateTableAsync<DenominacionConfig>();
                    await _database.CreateTableAsync<Nota>();

                    // Verificar existencia de la tabla "Notas"
                    var existeNotas = await _database.ExecuteScalarAsync<int>(
                        "SELECT count(1) FROM sqlite_master WHERE type='table' AND name='Notas'");
                    Debug.WriteLine($"[DatabaseService] Tabla 'Notas' existe: { (existeNotas > 0 ? "SI" : "NO") }");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DatabaseService] Error inicializando BD: {ex}");
                    throw;
                }
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
                registro.SesionId = SesionService.Instance.SesionActualId;

            if (registro.Id != 0)
                return await db.UpdateAsync(registro);
            else
                return await db.InsertAsync(registro);
        }

        public async Task<List<CajaRegistro>> ObtenerCajasAsync()
        {
            var db = await GetDatabaseAsync();
            int sesionId = SesionService.Instance.SesionActualId;
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
                movimiento.SesionId = SesionService.Instance.SesionActualId;

            if (movimiento.Id != 0)
                return await db.UpdateAsync(movimiento);
            else
                return await db.InsertAsync(movimiento);
        }

        public async Task<List<MovimientoEfectivo>> ObtenerMovimientosAsync()
        {
            var db = await GetDatabaseAsync();
            int sesionId = SesionService.Instance.SesionActualId;
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
            int sesionId = SesionService.Instance.SesionActualId;
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
                voucher.SesionId = SesionService.Instance.SesionActualId;

            if (voucher.Id != 0)
                return await db.UpdateAsync(voucher);
            else
                return await db.InsertAsync(voucher);
        }

        public async Task<List<Voucher>> ObtenerVouchersAsync()
        {
            var db = await GetDatabaseAsync();
            int sesionId = SesionService.Instance.SesionActualId;
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
                nota.SesionId = SesionService.Instance.SesionActualId;

            if (nota.Id != 0)
                return await db.UpdateAsync(nota);
            else
                return await db.InsertAsync(nota);
        }

        public async Task<List<Nota>> ObtenerNotasAsync()
        {
            var db = await GetDatabaseAsync();
            int sesionId = SesionService.Instance.SesionActualId;
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

            // Cajas registradas
            var totalCajas = await db.Table<CajaRegistro>().CountAsync();
            resultado["CajasRegistradas"] = totalCajas;

            // Movimientos de efectivo
            var totalMovimientos = await db.Table<MovimientoEfectivo>().CountAsync();
            resultado["MovimientosEfectivo"] = totalMovimientos;

            // Vouchers escaneados
            var totalVouchers = await db.Table<Voucher>().CountAsync();
            resultado["VouchersEscaneados"] = totalVouchers;

            // Notas creadas
            var totalNotas = await db.Table<Nota>().CountAsync();
            resultado["NotasCreadas"] = totalNotas;

            // Notas favoritas
            var totalFavoritas = await db.Table<Nota>().Where(n => n.EsFavorita).CountAsync();
            resultado["NotasFavoritas"] = totalFavoritas;

            // Notas con imagen
            var totalConImagen = await db.Table<Nota>().Where(n => n.TieneImagen).CountAsync();
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
        #endregion
    }
}
#endregion