using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Events;
namespace Bdgrz.Compliance.Features.Tenants.Reactors;

/// <summary>
///     Carries each answer of the slug owner back to the tenant it answers. Every answer is the exact logical
///     operation its source fact causes; the tenant treats a replayed or late answer as a terminal no-op, so this
///     reactor never fails on a stale outcome and the global checkpoint always advances.
/// </summary>
public sealed partial class TenantSlugReactor(
    IProjectionCheckpointStore checkpoints,
    IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "tenant-slugs")),
      IReactorHandler<TenantSlugRegistered>,
      IReactorHandler<TenantSlugRegistrationRejected>,
      IReactorHandler<TenantSlugSurrendered>,
      IReactorHandler<TenantSlugSurrenderRejected>
{
    public ValueTask HandleAsync(IReactorContext<TenantSlugRegistered> context, CancellationToken ct) =>
        bus.SendReactionAsync(TenantSlugEffects.Confirm(context.Trigger.Metadata.EventId, context.Trigger.TenantId,
            context.Trigger.Slug), context, ct);

    public ValueTask HandleAsync(
        IReactorContext<TenantSlugRegistrationRejected> context,
        CancellationToken ct) => bus.SendReactionAsync(TenantSlugEffects.Reject(context.Trigger.Metadata.EventId,
        context.Trigger.TenantId, context.Trigger.Slug), context, ct);

    public ValueTask HandleAsync(IReactorContext<TenantSlugSurrendered> context, CancellationToken ct) =>
        bus.SendReactionAsync(TenantSlugEffects.ConfirmSurrender(context.Trigger.Metadata.EventId,
            context.Trigger.TenantId, context.Trigger.Slug), context, ct);

    public ValueTask HandleAsync(IReactorContext<TenantSlugSurrenderRejected> context, CancellationToken ct) =>
        bus.SendReactionAsync(TenantSlugEffects.RejectSurrender(context.Trigger.Metadata.EventId,
            context.Trigger.TenantId, context.Trigger.Slug), context, ct);
}
