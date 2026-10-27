using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Reactors;

/// <summary>Reevaluates accepted engagements when one staff record's status changes.</summary>
sealed partial class FirmStaffStatusReevaluationReactor(IProjectionCheckpointStore checkpoints,
    ITenantDirectory tenants, IAggregateReader reader, FirmStaffViews staffViews,
    DirectoryReevaluationDiscovery discovery, IDomainEventReader events, ISourceFactResolver sources,
    IImmutableContentStore content)
    : Reactor(checkpoints, EventStreamPattern.ForPattern(FirmStaffContent.Realm, FirmStaffRecord.Area), WorkloadName),
        IReactorHandler<FirmStaffChangeRecorded>
{
    public const string WorkloadName = "FirmStaffStatusReevaluationV1";

    public async ValueTask HandleAsync(IReactorContext<FirmStaffChangeRecorded> context, CancellationToken ct)
    {
        await FirmStaffSource.VerifyAsync(sources, content, context.Source.Stream, context.Source.ResourceOffset,
            context.Trigger, ct).ConfigureAwait(false);
        if (!Uuid.TryParse(context.Source.Stream.Resource, null, out var staffMemberId) ||
            context.Source.Stream != new FirmStaffRecord(staffMemberId).Stream)
            throw new InvalidOperationException("Staff status reconciliation requires its authoritative record stream.");
        if (context.Trigger.ChangeKind != "status")
            return;
        var record = await reader.HydrateAsync(new FirmStaffRecord(staffMemberId), ct).ConfigureAwait(false);
        var history = await FirmStaffHistory.ReadAsync(events, record, ct).ConfigureAwait(false);
        if (!history.Matches(context.Trigger))
            throw new InvalidOperationException("Staff status reconciliation requires its retained original record decision.");
        var changed = await staffViews.ReadAsync(context.Trigger, ct).ConfigureAwait(false);
        var seen = new HashSet<Uuid>();
        await foreach (var tenant in tenants.GetActiveTenantsAsync(ct).WithCancellation(ct).ConfigureAwait(false))
        {
            if (!Uuid.TryParse(tenant.Value, null, out var tenantId) || tenantId == Uuid.Empty)
                throw new InvalidOperationException("Registered assignment discovery requires canonical tenant identities.");
            if (seen.Add(tenantId))
                await discovery.ReconcileStatusAsync(tenantId, context, changed, ct).ConfigureAwait(false);
        }
    }
}
