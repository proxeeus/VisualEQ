using System;
using System.Data;
using VisualEQ.Database.Configuration;

namespace VisualEQ.Database.Repositories.Base
{
    public abstract class RepositoryBase
    {
        protected readonly IDbConnectionFactory ConnectionFactory;

        protected RepositoryBase(IDbConnectionFactory connectionFactory)
        {
            ConnectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        protected IDbConnection CreateConnection() => ConnectionFactory.CreateConnection();
    }
} 