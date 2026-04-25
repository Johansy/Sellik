// Services/LicenseService.cs
// Gestión del plan Free / Premium con Mercado PAgo como pasarela de pago.
// Validación de claves 100% offline mediante HMAC-SHA256.
// En DEBUG siempre devuelve Premium para no interferir con el desarrollo.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace CajaApp.Services
{
    // ── Configuración M.P ─────────────────────────────────────────────────────
    public static class PayPalConfig
    {
        public static string CheckoutUrlUnico   => AppSecrets.GetPayPalUnicoUrl();
        public static string CheckoutUrlMensual => AppSecrets.GetPayPalMensualUrl();
    }

    public enum PlanTipo { Free, Premium }
    public enum TipoSuscripcion { Ninguna, Mensual, Anual }

    public class LicenseService : INotifyPropertyChanged
    {
        private const string StorageKey = "ls_license_key";
        private const int    LimiteVouchersFree = 300;
        private static readonly DateTime EpochBase = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static LicenseService? _instance;
        public static LicenseService Instance => _instance ??= new LicenseService();

        private PlanTipo        _plan = PlanTipo.Free;
        private string?         _licenseKey;
        private bool            _validando;
        private TipoSuscripcion _suscripcion = TipoSuscripcion.Ninguna;
        private DateTime?       _fechaExpiracion;

        private LicenseService() { }

        // ── Estado público ─────────────────────────────────────────────────────

        public PlanTipo Plan
        {
            get => _plan;
            private set
            {
                if (_plan != value)
                {
                    _plan = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EsPremium));
                    OnPropertyChanged(nameof(EsFree));
                    PlanCambiado?.Invoke(this, value);
                }
            }
        }

        public bool EsPremium => Plan == PlanTipo.Premium
                                  && (_fechaExpiracion == null || DateTime.UtcNow <= _fechaExpiracion);
        public bool EsFree    => !EsPremium;

        public TipoSuscripcion Suscripcion
        {
            get => _suscripcion;
            private set { _suscripcion = value; OnPropertyChanged(); }
        }

        public DateTime? FechaExpiracion
        {
            get => _fechaExpiracion;
            private set { _fechaExpiracion = value; OnPropertyChanged(); OnPropertyChanged(nameof(DiasRestantes)); }
        }

        /// Días que faltan para que expire la licencia. Negativo = ya venció.
        public int? DiasRestantes => _fechaExpiracion.HasValue
            ? (int)Math.Floor((_fechaExpiracion.Value - DateTime.UtcNow).TotalDays)
            : (int?)null;

        public bool Validando
        {
            get => _validando;
            private set { _validando = value; OnPropertyChanged(); }
        }

        public string? LicenseKey => _licenseKey;

        public int LimiteVouchers => LimiteVouchersFree;

        public event EventHandler<PlanTipo>? PlanCambiado;
        public event PropertyChangedEventHandler? PropertyChanged;

        // ── Inicialización (llamar al arrancar la app) ─────────────────────────

        public async Task InicializarAsync()
        {
#if DEBUG
            Plan = PlanTipo.Premium;
            return;
#endif
            var key = await SecureStorage.GetAsync(StorageKey);
            if (!string.IsNullOrWhiteSpace(key))
                await ValidarYAplicarAsync(key, silencioso: true);
            else
                Plan = PlanTipo.Free;
        }

        // ── Activar con clave manual (el usuario la pega desde el email) ───────

        public async Task<(bool Ok, string Mensaje)> ActivarClaveAsync(string licenseKey)
        {
#if DEBUG
            Plan = PlanTipo.Premium;
            return (true, LocalizationService.Get("License_DebugActivado"));
#endif
            if (string.IsNullOrWhiteSpace(licenseKey))
                return (false, LocalizationService.Get("License_ClaveVacia"));

            return await ValidarYAplicarAsync(licenseKey.Trim(), silencioso: false);
        }

        // ── Restaurar compra (re-valida la clave guardada) ─────────────────────

        public async Task<(bool Ok, string Mensaje)> RestaurarCompraAsync()
        {
#if DEBUG
            Plan = PlanTipo.Premium;
            return (true, LocalizationService.Get("License_DebugRestaurado"));
#endif
            var key = await SecureStorage.GetAsync(StorageKey);
            if (string.IsNullOrWhiteSpace(key))
                return (false, LocalizationService.Get("License_NoEncontrada"));

            return await ValidarYAplicarAsync(key, silencioso: false);
        }

        // ── Revocar / desactivar ───────────────────────────────────────────────

        public async Task DesactivarAsync()
        {
            SecureStorage.Remove(StorageKey);
            _licenseKey     = null;
            Suscripcion     = TipoSuscripcion.Ninguna;
            FechaExpiracion = null;
            Plan            = PlanTipo.Free;
            await Task.CompletedTask;
        }

        // ── Restricciones Free ─────────────────────────────────────────────────

        /// Verifica si se puede escanear un voucher más (límite 300 en Free).
        public async Task<bool> PuedeEscanearVoucherAsync(DatabaseService db)
        {
#if DEBUG
            return true;
#endif
            if (EsPremium) return true;
            var total = await db.ContarVouchersTotalAsync();
            return total < LimiteVouchersFree;
        }

        /// Verifica si se puede crear una nueva sesión (límite 1 activa en Free).
        public async Task<bool> PuedeCrearSesionAsync(DatabaseService db)
        {
#if DEBUG
            return true;
#endif
            if (EsPremium) return true;
            var total = await db.ContarSesionesAsync();
            return total < 1;
        }

        /// Verifica si se puede exportar (solo Premium).
        public bool PuedeExportar()
        {
#if DEBUG
            return true;
#endif
            return EsPremium;
        }

        /// Verifica si se puede ver el historial completo (solo Premium).
        public bool PuedeVerHistorialCompleto()
        {
#if DEBUG
            return true;
#endif
            return EsPremium;
        }

        // ── Validación HMAC offline ────────────────────────────────────────────

        private async Task<(bool Ok, string Mensaje)> ValidarYAplicarAsync(string key, bool silencioso)
        {
            Validando = true;
            try
            {
                await Task.CompletedTask; // mantiene la firma async

                var (tipo, fechaEmision) = DecodificarClave(key);

                if (tipo != TipoSuscripcion.Ninguna && fechaEmision.HasValue)
                {
                    var duracion     = tipo == TipoSuscripcion.Mensual ? 30 : 365;
                    var expiracion   = fechaEmision.Value.AddDays(duracion);

                    if (DateTime.UtcNow > expiracion)
                    {
                        Plan            = PlanTipo.Free;
                        Suscripcion     = TipoSuscripcion.Ninguna;
                        FechaExpiracion = null;
                        return (false, silencioso ? string.Empty : LocalizationService.Get("License_Vencida"));
                    }

                    _licenseKey     = key;
                    await SecureStorage.SetAsync(StorageKey, key);
                    Suscripcion     = tipo;
                    FechaExpiracion = expiracion;
                    Plan            = PlanTipo.Premium;

                    var claveActivada = tipo == TipoSuscripcion.Mensual ? "License_ActivadaMensual" : "License_ActivadaAnual";
                    return (true, LocalizationService.GetF(claveActivada, expiracion.ToString("dd/MM/yyyy")));
                }
                else
                {
                    Plan            = PlanTipo.Free;
                    Suscripcion     = TipoSuscripcion.Ninguna;
                    FechaExpiracion = null;
                    return (false, silencioso ? string.Empty : LocalizationService.Get("License_Invalida"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseService] Error: {ex.Message}");
                Plan            = PlanTipo.Free;
                Suscripcion     = TipoSuscripcion.Ninguna;
                FechaExpiracion = null;
                return (false, silencioso ? string.Empty : LocalizationService.Get("License_ErrorValidar"));
            }
            finally
            {
                Validando = false;
            }
        }

        // ── Lógica HMAC ────────────────────────────────────────────────────────

        /// <summary>
        /// Decodifica y verifica la clave. Devuelve el tipo de suscripción y la fecha de emisión
        /// si la clave es válida, o (Ninguna, null) si es inválida.
        /// Formato: SELLIK-TTDD-DDSS-CCCC
        ///   TT   = tipo  (01=Mensual, 02=Anual) — 1 byte hex
        ///   DDDD = días desde 2024-01-01 big-endian uint16 — 2 bytes hex
        ///   SS   = salt aleatorio — 1 byte hex
        ///   CCCC = checksum HMAC-SHA256 de los 8 chars anteriores
        /// </summary>
        private static (TipoSuscripcion Tipo, DateTime? FechaEmision) DecodificarClave(string key)
        {
            var ninguna = (TipoSuscripcion.Ninguna, (DateTime?)null);
            if (string.IsNullOrWhiteSpace(key)) return ninguna;

            key = key.Trim().ToUpperInvariant();
            var parts = key.Split('-');
            if (parts.Length != 4 || parts[0] != "SELLIK") return ninguna;
            if (parts[1].Length != 4 || parts[2].Length != 4 || parts[3].Length != 4) return ninguna;

            var payload  = parts[1] + parts[2];   // 8 hex chars = 4 bytes
            var checksum = parts[3];

            if (ComputeChecksum(payload) != checksum) return ninguna;

            // Byte 0: tipo
            var tipoByte = Convert.ToByte(payload[..2], 16);
            TipoSuscripcion tipo = tipoByte switch
            {
                0x01 => TipoSuscripcion.Mensual,
                0x02 => TipoSuscripcion.Anual,
                _    => TipoSuscripcion.Ninguna
            };
            if (tipo == TipoSuscripcion.Ninguna) return ninguna;

            // Bytes 1-2: días desde epoch (big-endian uint16)
            var diasBytes = new byte[] { Convert.ToByte(payload[2..4], 16), Convert.ToByte(payload[4..6], 16) };
            if (BitConverter.IsLittleEndian) Array.Reverse(diasBytes);
            var dias = BitConverter.ToUInt16(diasBytes, 0);
            var fechaEmision = EpochBase.AddDays(dias);

            return (tipo, fechaEmision);
        }

        private static string ComputeChecksum(string payload)
        {
            var secret = Encoding.UTF8.GetBytes(AppSecrets.GetLicenseSecret());
            var data   = Encoding.UTF8.GetBytes(payload);
            using var hmac = new HMACSHA256(secret);
            var hash = hmac.ComputeHash(data);
            return BitConverter.ToString(hash, 0, 3).Replace("-", "")[..4];
        }

        /// <summary>
        /// Genera una nueva clave de licencia para el tipo de suscripción indicado.
        /// </summary>
        public static string GenerarClave(TipoSuscripcion tipo)
        {
            if (tipo == TipoSuscripcion.Ninguna)
                throw new ArgumentException("El tipo de suscripción no puede ser Ninguna.");

            byte tipoByte = tipo == TipoSuscripcion.Mensual ? (byte)0x01 : (byte)0x02;

            var diasDesdeEpoch = (ushort)(DateTime.UtcNow - EpochBase).TotalDays;
            var diasBytes = BitConverter.GetBytes(diasDesdeEpoch);
            if (BitConverter.IsLittleEndian) Array.Reverse(diasBytes);  // big-endian

            var salt    = RandomNumberGenerator.GetBytes(1)[0];
            var payload = $"{tipoByte:X2}{diasBytes[0]:X2}{diasBytes[1]:X2}{salt:X2}";
            var checksum = ComputeChecksum(payload);
            return $"SELLIK-{payload[..4]}-{payload[4..]}-{checksum}";
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
