using Microsoft.Extensions.Configuration;

namespace Bdgrz.Compliance.Hosting.Persistence;

static class CassieReadModelSettings
{
    public static string ConnectionString(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var connectionString = configuration["Cassie:ConnectionString"]
            ?? Environment.GetEnvironmentVariable("Cassie__ConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Configure Cassie:ConnectionString for Compliance read models.");
        return connectionString;
    }
}
