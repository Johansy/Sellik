using CajaApp.Models;
using SkiaSharp;

namespace CajaApp.Services
{
    public class ImagenService
    {
        private readonly string _carpetaImagenes;

        public ImagenService()
        {
            _carpetaImagenes = Path.Combine(FileSystem.AppDataDirectory, "ImagenesNotas");
            if (!Directory.Exists(_carpetaImagenes))
            {
                Directory.CreateDirectory(_carpetaImagenes);
            }
        }

        public async Task<string> GuardarImagenAsync(byte[] imagenBytes, string extension = ".jpg")
        {
            try
            {
                string nombreArchivo = $"nota_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";
                string rutaCompleta = Path.Combine(_carpetaImagenes, nombreArchivo);

                await File.WriteAllBytesAsync(rutaCompleta, imagenBytes);

                return rutaCompleta;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al guardar imagen: {ex.Message}");
            }
        }

        public async Task<string> GuardarImagenDesdeStreamAsync(Stream stream, string extension = ".jpg")
        {
            try
            {
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                return await GuardarImagenAsync(memoryStream.ToArray(), extension);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al procesar imagen: {ex.Message}");
            }
        }

        public async Task<byte[]?> ObtenerImagenAsync(string rutaImagen)
        {
            try
            {
                if (File.Exists(rutaImagen))
                    return await File.ReadAllBytesAsync(rutaImagen);

                // Fallback: reconstruir la ruta desde el nombre de archivo.
                // Cubre el caso en que la ruta absoluta guardada quedó obsoleta
                // tras una reinstalación (p.ej. cambio de UUID en iOS).
                var nombreArchivo = Path.GetFileName(rutaImagen);
                if (!string.IsNullOrEmpty(nombreArchivo))
                {
                    var rutaReconstruida = Path.Combine(_carpetaImagenes, nombreArchivo);
                    if (File.Exists(rutaReconstruida))
                        return await File.ReadAllBytesAsync(rutaReconstruida);
                }

                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener imagen: {ex.Message}");
            }
        }

        public bool EliminarImagen(string rutaImagen)
        {
            try
            {
                var rutaFinal = rutaImagen;

                if (!File.Exists(rutaFinal))
                {
                    var nombreArchivo = Path.GetFileName(rutaImagen);
                    if (!string.IsNullOrEmpty(nombreArchivo))
                    {
                        var rutaReconstruida = Path.Combine(_carpetaImagenes, nombreArchivo);
                        if (File.Exists(rutaReconstruida))
                            rutaFinal = rutaReconstruida;
                    }
                }

                if (File.Exists(rutaFinal))
                {
                    File.Delete(rutaFinal);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> RedimensionarImagenAsync(string rutaOriginal, int maxWidth = 1600, int maxHeight = 1200)
        {
            try
            {
                var imagenBytes = await File.ReadAllBytesAsync(rutaOriginal);
                var bytesRedimensionados = RedimensionarBytes(imagenBytes, maxWidth, maxHeight);

                string nombreRedimensionado = Path.GetFileNameWithoutExtension(rutaOriginal) + "_resized.jpg";
                string rutaRedimensionada = Path.Combine(_carpetaImagenes, nombreRedimensionado);

                await File.WriteAllBytesAsync(rutaRedimensionada, bytesRedimensionados);

                return rutaRedimensionada;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al redimensionar imagen: {ex.Message}");
            }
        }

    
        /// Redimensiona un arreglo de bytes de imagen al máximo indicado manteniendo la proporción.
        /// Usar antes de enviar al motor OCR.
        public byte[] RedimensionarBytes(byte[] imagenBytes, int maxWidth = 1600, int maxHeight = 1200)
        {
            using var original = SKBitmap.Decode(imagenBytes);
            if (original is null)
                throw new InvalidOperationException("No se pudo decodificar la imagen.");

            // Si ya cabe dentro del límite, devolver el JPEG recodificado sin escalar
            if (original.Width <= maxWidth && original.Height <= maxHeight)
            {
                using var sinCambiosImage = SKImage.FromBitmap(original);
                return sinCambiosImage.Encode(SKEncodedImageFormat.Jpeg, 90).ToArray();
            }

            // Calcular escala proporcional (el lado más restrictivo manda)
            float escala = Math.Min((float)maxWidth / original.Width, (float)maxHeight / original.Height);
            int nuevoAncho = (int)(original.Width * escala);
            int nuevoAlto = (int)(original.Height * escala);

            using var redimensionado = original.Resize(new SKImageInfo(nuevoAncho, nuevoAlto), SKSamplingOptions.Default);
            if (redimensionado is null)
                throw new InvalidOperationException("Error al redimensionar el bitmap.");

            using var imagen = SKImage.FromBitmap(redimensionado);
            return imagen.Encode(SKEncodedImageFormat.Jpeg, 90).ToArray();
        }

        /// Redimensiona desde un Stream. Útil para preprocesar antes de OCR sin guardar en disco.
        public async Task<byte[]> RedimensionarDesdeStreamAsync(Stream stream, int maxWidth = 1600, int maxHeight = 1200)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return RedimensionarBytes(ms.ToArray(), maxWidth, maxHeight);
        }

        public long ObtenerTamanoDirectorio()
        {
            try
            {
                if (!Directory.Exists(_carpetaImagenes))
                    return 0;

                return Directory.GetFiles(_carpetaImagenes)
                    .Select(f => new FileInfo(f))
                    .Sum(fi => fi.Length);
            }
            catch
            {
                return 0;
            }
        }

        public int ContarImagenes()
        {
            try
            {
                if (!Directory.Exists(_carpetaImagenes))
                    return 0;

                return Directory.GetFiles(_carpetaImagenes, "*.jpg").Length +
                       Directory.GetFiles(_carpetaImagenes, "*.png").Length +
                       Directory.GetFiles(_carpetaImagenes, "*.jpeg").Length;
            }
            catch
            {
                return 0;
            }
        }

        public void LimpiarImagenesHuerfanas(List<string> rutasEnUso)
        {
            try
            {
                if (!Directory.Exists(_carpetaImagenes))
                    return;

                var archivosEnDirectorio = Directory.GetFiles(_carpetaImagenes);

                foreach (var archivo in archivosEnDirectorio)
                {
                    if (!rutasEnUso.Contains(archivo))
                    {
                        File.Delete(archivo);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error limpiando imágenes: {ex.Message}");
            }
        }
    }
}