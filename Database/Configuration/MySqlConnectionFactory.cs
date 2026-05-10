using System;
using System.Data;
using System.Threading.Tasks;
using MySqlConnector;

namespace VisualEQ.Database.Configuration
{
    public class MySqlConnectionFactory : IDbConnectionFactory
    {
        private readonly DatabaseSettings _settings;
        private readonly string _connectionString;

        public MySqlConnectionFactory(DatabaseSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _connectionString = BuildConnectionString();
        }

        public IDbConnection CreateConnection()
        {
            return new MySqlConnection(_connectionString);
        }

        public async Task<(bool Success, string Error)> TestConnectionAsync()
        {
            try
            {
                using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    return (true, null);
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private string BuildConnectionString()
        {
            var builder = new MySqlConnectionStringBuilder
            {
                Server = _settings.Server,
                Database = _settings.Database,
                UserID = _settings.Username,
                Password = _settings.Password,
                Port = (uint)_settings.Port,
                ConnectionTimeout = (uint)_settings.ConnectionTimeout
            };
            
            return builder.ConnectionString;
        }
    }
} 