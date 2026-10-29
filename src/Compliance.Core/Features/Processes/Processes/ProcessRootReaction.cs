using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Tenants.Reactors;
using Bdgrz.Compliance.Features.UserIdentities.Handlers;
namespace Bdgrz.Compliance.Features.Processes.Processes;

/// <summary>
/// What a root-fact reactor does: verify the committed source is a native fact of a root kind of its process, make the
/// process record exist (idempotently) before the source checkpoint can advance, then drive it. A failure before
/// the record exists is transient so the reactor retries without advancing its checkpoint; a failure after the
/// record exists is left to the record's own due time and the sweep.
/// </summary>
public sealed class ProcessRootReaction(IProcessRecordStore store, ProcessDriver driver, ProcessCatalog catalog,
    TimeProvider clock)
{
    /// <summary>Creates the record for a committed root fact and drives it once.</summary>
    public async Task<ProcessRecord> StartAsync(string processName, IReactorContext context, ImmutableContentRef intent,
        IEnumerable<string> targets, string worker, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(intent);
        var definition = catalog.Get(processName);
        var source = context.Source.Event;
        if (source is not INativeOperationFact native)
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Validation,
                "A process can only start from a native operation fact.", false));
        var kind = Bdgrz.Compliance.Features.SourceFacts.History.EventKind.Of(source);
        if (!definition.RootSourceEvents.Any(root => kind.StartsWith(root, StringComparison.Ordinal))
            && !definition.RootSourceEvents.Contains(source.GetType().Name, StringComparer.Ordinal))
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Validation,
                $"{source.GetType().Name} does not start {processName}.", false));
        var first = NativeFactReference.Of(context.Source.Stream, source);
        var key = ProcessKey.ForRoot(processName, native.Operation.RootOperationId, first);
        ProcessRecord record;
        try
        {
            record = await store.CreateIfAbsentAsync(ProcessRecord.Start(key,
                first, native.Operation.Episode, intent, targets,
                clock.GetUtcNow()), ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // No recoverable record exists yet, so the source checkpoint must not advance.
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The process record for {key} could not be created: {exception.Message}", true));
        }

        return await driver.RunAsync(key, worker, context, ct).ConfigureAwait(false);
    }
}
