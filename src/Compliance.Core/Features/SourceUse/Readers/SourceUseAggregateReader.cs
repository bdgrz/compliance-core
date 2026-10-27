using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.History;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.SourceUse.Readers;

/// <summary>
/// Finalizes source-use owners at the exact hydrated cut. Full transient validation protects unselected historical
/// fences as well as selected ones. Its inner reader is the original keyed registration, never this decorator.
/// </summary>
public sealed class SourceUseAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    /// <inheritdoc />
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (aggregate is not ISourceUseOwner source)
            return await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        source.Uses.BeginHydration();
        var owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        var history = new SourceUseHistoryValidation(owner.Stream);
        if (owner.Version == 0 && owner.CommittedStreamPosition == 0)
        {
            source.Uses.Confirm(history, 0, 0);
            return owner;
        }
        var position = 0UL;
        var version = 0UL;
        var ids = new HashSet<Uuid>();
        await foreach (var record in events.ReadAsync(owner.Stream, 0, ct).ConfigureAwait(false))
        {
            if (record.ResourceOffset >= owner.CommittedStreamPosition)
                break;
            var metadata = record.Event.Metadata;
            if (record.Stream != owner.Stream || record.ResourceOffset != position || metadata.IsAudit ||
                metadata.AggregateId != owner.Id || metadata.AggregateVersion != checked(version + 1) ||
                metadata.EventId == Uuid.Empty || !ids.Add(metadata.EventId))
                throw new ImmutableContentIntegrityException("The source-use history has another exact owner, physical offset or metadata version.");
            history.ObserveOwnerFact(record.Event);
            if (SourceUseFactKinds.Classify(record.Event) is { } kind)
                history.Observe(kind.Purpose, kind.Granted, (ISourceUseFact)record.Event);
            position = checked(record.ResourceOffset + 1);
            version = metadata.AggregateVersion;
        }
        if (position != owner.CommittedStreamPosition || version != owner.Version)
            throw new ImmutableContentUnavailableException("The source-use owner's exact hydrated history cut is not visible yet.");
        ct.ThrowIfCancellationRequested();
        source.Uses.Confirm(history, owner.Version, owner.CommittedStreamPosition);
        return owner;
    }
}
