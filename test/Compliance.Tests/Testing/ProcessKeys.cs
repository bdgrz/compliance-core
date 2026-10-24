using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Finds a test process key from its committed native root, including its source identity.</summary>
static class ProcessKeys
{
    public static async Task<ProcessKey> FindAsync(IServiceProvider provider, string process, string realm, Uuid operationId)
    {
        await using var scope = provider.CreateAsyncScope();
        var roots = scope.ServiceProvider.GetServices<ProcessDefinition>().Where(definition => definition.Name == process)
            .SelectMany(definition => definition.RootSourceEvents).ToHashSet(StringComparer.Ordinal);
        NativeFactReference? source = null;
        await foreach (var record in scope.ServiceProvider.GetRequiredService<IDomainEventReader>().ReadAsync(
                           EventStreamPattern.ForPattern(realm), EventCursor.Start, CancellationToken.None))
        {
            if (!roots.Contains(record.Event.GetType().Name) || record.Event is not INativeOperationFact fact ||
                fact.Operation.RootOperationId != operationId)
                continue;
            if (source is not null)
                throw new InvalidOperationException("The test must select one committed process root.");
            source = NativeFactReference.Of(record.Stream, record.Event);
        }
        return ProcessKey.ForRoot(process, operationId,
            source ?? throw new InvalidOperationException("The process root was not committed."));
    }
}
