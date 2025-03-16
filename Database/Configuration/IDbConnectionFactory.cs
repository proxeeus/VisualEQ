using System.Data;
using System.Threading.Tasks;

namespace VisualEQ.Database.Configuration
{
    public interface IDbConnectionFactory
    {
        IDbConnection CreateConnection();
        Task<bool> TestConnectionAsync();
    }
} 