using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.Providers;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Providers;

/// <summary>Projects the applied provider facts of a tenant into the provider directory, as the projector would.</summary>
static class ProviderProjectionHelper
{
    public static async Task CatchUpAsync(IServiceProvider provider, Uuid tenant)
    {
        using var scope = provider.CreateScope();
        var directory = scope.ServiceProvider.GetRequiredService<FitzProviderDirectory>();
        var events = scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var checkpoint = await directory.LoadCheckpointAsync(tenant);
        var pattern = EventStreamPattern.ForPattern(tenant.ToString(), Provider.Area);
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity(FitzProviderDirectory.ProjectorName, pattern), checkpoint));
        var cursor = checkpoint.Cursor;
        await foreach (var record in events.ReadAsync(pattern, checkpoint.Cursor, CancellationToken.None))
        {
            if (record.Event is INamedRecordAppliedFact applied)
            {
                var bytes = await content.ReadVerifiedAsync(tenant.ToString(), applied.ContentContent);
                await directory.ApplyAsync(record.Event, JsonSerializer.Deserialize(bytes.Span,
                    ComplianceCoreJsonContext.Default.ProviderContent)!);
            }

            cursor = record.NextCursor;
        }

        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
    }
}
