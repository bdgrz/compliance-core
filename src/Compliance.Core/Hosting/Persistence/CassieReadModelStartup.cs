using Cntryl.Cassie.Connection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Hosting.Persistence;

sealed class CassieReadModelStartup(CassieDataSource source) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        CassieReadModelSchema.InitializeAsync(source, cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
