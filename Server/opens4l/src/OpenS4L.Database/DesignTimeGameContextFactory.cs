using Hjson;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using OpenS4L.Common.Configuration;

namespace OpenS4L.Database
{
    public class DesignTimeGameContextFactory : IDesignTimeDbContextFactory<GameContext>
    {
        public GameContext CreateDbContext(string[] args)
        {
            var connectionString =
                HjsonValue.Load("config.hjson")
                    ["Database"]
                    [nameof(DatabaseOptions.ConnectionStrings)]
                    [nameof(ConnectionStrings.Game)]
                    .ToValue().ToString();

            return new GameContext(
                new DbContextOptionsBuilder<GameContext>()
                    .UseNpgsql(connectionString)
                    .Options
            );
        }
    }
}
