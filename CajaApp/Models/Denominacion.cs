using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace CajaApp.Models
{
    public class Denominacion : INotifyPropertyChanged
    {
        private int _cantidad;

        public decimal Valor { get; set; }
        public string Simbolo { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public TipoDenominacion Tipo { get; set; }
        public int Cantidad
        {
            get => _cantidad;
            set
            {
                _cantidad = value;
                OnPropertyChanged(nameof(Cantidad));
                OnPropertyChanged(nameof(SubTotal));
                OnPropertyChanged(nameof(SubtotalTexto));
            }
        }
        
        public decimal SubTotal => Valor * Cantidad;

        public string ValorTexto => Tipo == TipoDenominacion.Moneda ? $"${Valor:F2}" : $"${Valor:F0}";

        public string SubtotalTexto => $"${SubTotal:F2}";

        public Microsoft.Maui.Graphics.Color ColorValue
        {
            get
            {
                try { return Microsoft.Maui.Graphics.Color.FromArgb(Color); }
                catch { return Microsoft.Maui.Graphics.Color.FromArgb("#9E9E9E"); }
            }
        }
      
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }   
    }

    public enum TipoDenominacion
    {
        Moneda,
        Billete
    }
}
