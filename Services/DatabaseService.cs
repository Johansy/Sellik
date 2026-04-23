using SQLite;

public class DatabaseService
{
    readonly SQLiteAsyncConnection _db;

    public DatabaseService(string dbPath)
    {
        _db = new SQLiteAsyncConnection(dbPath);
    }

    public async Task InitializeAsync()
    {
        // Crea la tabla 'Notas' si no existe
        await _db.CreateTableAsync<Nota>();
    }

    public Task<List<Nota>> GetAllNotasAsync() =>
        _db.Table<Nota>().ToListAsync();
}

public class Nota
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Texto { get; set; }
}