using System;

namespace VisualEQ.Database.Configuration
{
    public class DatabaseSettings
    {
        public string Server { get; set; }
        public string Database { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public int Port { get; set; }
        public int ConnectionTimeout { get; set; }
    }
} 