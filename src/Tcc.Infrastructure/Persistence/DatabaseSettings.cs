using System.Text.Json.Serialization;
using Npgsql;

namespace Tcc.Infrastructure.Persistence;

public sealed class DatabaseSettings
{
    private DatabaseSettings(string connectionString) => ConnectionString = connectionString;
    [JsonIgnore] public string ConnectionString { get; }
    public static DatabaseSettings Read(Func<string, string?> readVariable)
    {
        try
        {
            var raw = readVariable("ConnectionStrings__TccDatabase");
            if (string.IsNullOrWhiteSpace(raw)) throw new ArgumentException();
            var builder = new NpgsqlConnectionStringBuilder(raw) { IncludeErrorDetail = false };
            if (string.IsNullOrWhiteSpace(builder.Host) || string.IsNullOrWhiteSpace(builder.Database)
                || string.IsNullOrWhiteSpace(builder.Username)) throw new ArgumentException();
            return new(builder.ConnectionString);
        }
        catch (Exception) { throw new InvalidOperationException("Configure ConnectionStrings__TccDatabase no ambiente do backend."); }
    }
    public override string ToString() => "PostgreSQL configuration (redacted)";
}
