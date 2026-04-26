using SQLite;

namespace CajaApp.Models
{
    [Table("DenominacionValores")]
    public class DenominacionValor
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int CajaRegistroId { get; set; }
        public int DenominacionConfigId { get; set; }
        public int Cantidad { get; set; }
    }
}
