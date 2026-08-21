using Hjson;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using OpenS4L.Common.Configuration;

namespace OpenS4L.Database
{
    public class DesignTimeAuthContextFactory : IDesignTimeDbContextFactory<AuthContext>
    {
        public AuthContext CreateDbContext(string[] args)
        {
            var connectionString =
                HjsonValue.Load("config.hjson")
                    ["Database"]
                    [nameof(DatabaseOptions.ConnectionStrings)]
                    [nameof(ConnectionStrings.Auth)]
                    .ToValue().ToString();

            return new AuthContext(
                new DbContextOptionsBuilder<AuthContext>()
                    .UseNpgsql(connectionString)
                    .Options
            );
        }
    }
}
