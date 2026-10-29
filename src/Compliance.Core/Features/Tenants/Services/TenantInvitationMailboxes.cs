using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Features.Tenants.Services;

/// <summary>
///     Handler-internal use of the mailbox claim owner. The invitation handler acquires the claim under its own
///     first-owner admission before it publishes the invitation, and the accepting handler releases exactly the
///     invitation and generation it accepted. An unknown acquire outcome recovers by hydrating the claim, which
///     names the holding invitation: a retry of the same invitation id finds its own claim and continues. No
///     separate reactor exists for the claim.
/// </summary>
public sealed class TenantInvitationMailboxes(IAggregateReader reader, IAggregateExecutor executor,
    ITenantInvitationDeliveryStore deliveries, TimeProvider clock)
{
    const int MaximumAttempts = 4;

    /// <summary>How long a claim whose invitation was never published blocks the mailbox.</summary>
    public static TimeSpan UnpublishedLease { get; } = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Claims the mailbox for the invitation and returns the claim generation. A holder that can no longer be
    ///     accepted, or whose delivery definitely failed, is superseded; any other holder conflicts.
    /// </summary>
    public async ValueTask<Result<long>> AcquireAsync(IExecutionContext context, Uuid tenantId, string emailAddress,
        Uuid invitationId, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var claim = await reader.HydrateAsync(new TenantInvitationMailboxClaim(tenantId, emailAddress), ct)
                .ConfigureAwait(false);
            var superseded = Uuid.Empty;
            var supersededGeneration = 0L;
            if (claim.Holder != Uuid.Empty && claim.Holder != invitationId)
            {
                if (!await IsUnusableAsync(claim, tenantId, ct).ConfigureAwait(false))
                    return Result<long>.Failure(TenantInvitationMailboxClaim.PendingInvitationConflict);
                superseded = claim.Holder;
                supersededGeneration = claim.HolderGeneration;
            }

            try
            {
                return await executor.ExecuteAsync<TenantInvitationMailboxClaim, long>(
                    new TenantInvitationMailboxClaim(tenantId, emailAddress), mailbox =>
                    {
                        var claimed = mailbox.Claim(invitationId, clock.GetUtcNow(), superseded, supersededGeneration);
                        return claimed.IsSuccess ? AggregateOutcome.Commit(claimed) : AggregateOutcome.Discard(claimed);
                    }, context, ct).ConfigureAwait(false);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // A concurrent claim or release won the append; the next attempt decides against its result.
            }
        }
    }

    /// <summary>Releases the mailbox when the exact invitation and generation still hold it; anything else is a no-op.</summary>
    public async ValueTask<Result> ReleaseAsync(IExecutionContext context, Uuid tenantId, string emailAddress,
        Uuid invitationId, long generation, string reason, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await executor.ExecuteAsync(new TenantInvitationMailboxClaim(tenantId, emailAddress),
                    mailbox => AggregateOutcome.CommitOnSuccess(
                        mailbox.Release(invitationId, generation, reason, clock.GetUtcNow())), context, ct)
                    .ConfigureAwait(false);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // Another claim or release moved the mailbox; the next attempt compares the exact holder again.
            }
        }
    }

    async ValueTask<bool> IsUnusableAsync(TenantInvitationMailboxClaim claim, Uuid tenantId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var holder = await reader.HydrateAsync(new TenantInvitation(tenantId, claim.Holder), ct).ConfigureAwait(false);
        if (!holder.IsCreated)
            return now >= claim.ClaimedAt + UnpublishedLease;
        if (holder.IsAccepted || holder.IsExpiredAt(now))
            return true;
        var delivery = await deliveries.ReadAsync(tenantId, claim.Holder, ct).ConfigureAwait(false);
        return delivery?.Status == TenantInvitationDeliveryRecord.Failed;
    }
}
