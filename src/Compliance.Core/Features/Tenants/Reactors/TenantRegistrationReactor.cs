using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Features.Tenants.Reactors;

/// <summary>
///     Starts the slug claims a tenant fact causes, and invites the first administrator. The tenant fact already
///     records its owner, so no separate owner relationship is registered. Every claim is the exact logical
///     operation its source fact causes, so a replay reuses it.
/// </summary>
public sealed partial class TenantRegistrationReactor(
    IProjectionCheckpointStore checkpoints,
    IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "tenants")),
      IReactorHandler<TenantRegistered>,
      IReactorHandler<TenantSlugChangeRequested>,
      IReactorHandler<TenantSlugChanged>,
      IReactorHandler<TenantSlugSurrenderRequested>
{
    public async ValueTask HandleAsync(IReactorContext<TenantRegistered> context, CancellationToken ct)
    {
        await bus.SendReactionAsync(
            TenantSlugEffects.Register(context.Trigger.Metadata.EventId, context.Trigger.TenantId,
                context.Trigger.Slug), context, ct);
        if (!context.Trigger.CreatorIsAdministrator &&
            context.Trigger.FirstAdministratorEmail is { } email)
            // The tenant fixes the invitation id, so a replay is an exact retry of the same invitation.
            await bus.SendReactionAsync(new InviteTenantMember(
                TenantInvitationIds.FirstAdministratorOf(context.Trigger.TenantId), context.Trigger.TenantId, email,
                "client_personnel", Administrator: true),
                context, ct);
    }

    public ValueTask HandleAsync(IReactorContext<TenantSlugSurrenderRequested> context, CancellationToken ct) =>
        bus.SendReactionAsync(
            TenantSlugEffects.Surrender(context.Trigger.Metadata.EventId, context.Trigger.TenantId,
                context.Trigger.Slug), context, ct);

    public ValueTask HandleAsync(IReactorContext<TenantSlugChangeRequested> context, CancellationToken ct) =>
        bus.SendReactionAsync(
            TenantSlugEffects.Register(context.Trigger.Metadata.EventId, context.Trigger.TenantId,
                context.Trigger.NewSlug), context, ct);

    public ValueTask HandleAsync(IReactorContext<TenantSlugChanged> context, CancellationToken ct) =>
        bus.SendReactionAsync(
            TenantSlugEffects.Surrender(context.Trigger.Metadata.EventId, context.Trigger.TenantId,
                context.Trigger.OldSlug), context, ct);
}
