using System.Data;
using System.Threading.Tasks;

namespace VisualEQ.Database.Configuration
{
    public interface IDbConnectionFactory
    {
        IDbConnection CreateConnection();
        // Returns (true, null) on success; (false, errorMessage) on failure.
        Task<(bool Success, string Error)> TestConnectionAsync();
    }
} 