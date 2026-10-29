using Cntryl.Fitz;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Contracts;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Responsibilities.Events;
namespace Bdgrz.Compliance.Features.Responsibilities.Services;

public sealed class FitzMemberResponsibilityIndex(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/member-responsibilities-v2/projection",
        ProjectorName), IMemberResponsibilityIndex
{
    public const string ProjectorName = "MemberResponsibilitiesV2";

    public async ValueTask<IReadOnlyList<ResponsibilityAssignmentView>> GetAsync(Uuid tenantId,
        Uuid memberId, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var member = await MemberResponsibilityIndexSchema.Members.GetAsync(tx, memberId, ct)
            .ConfigureAwait(false);
        return member is { TenantId: var storedTenant } && storedTenant == tenantId
            ? member.Assignments
            : [];
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case ResponsibilityAssigned assigned:
                await UpsertAssignmentAsync(assigned, ct).ConfigureAwait(false);
                break;
            case ResponsibilityRevoked revoked:
                await RevokeAssignmentAsync(revoked, ct).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(domainEvent));
        }
    }

    async ValueTask RevokeAssignmentAsync(ResponsibilityRevoked ev,
        CancellationToken ct)
    {
        var current = await MemberResponsibilityIndexSchema.Assignments.GetAsync(Transaction,
            MemberResponsibilityIndexSchema.AssignmentKey(ev.Scope, ev.AssignmentId), ct).ConfigureAwait(false) ?? throw new InvalidOperationException(
            "A responsibility revocation cannot project before its assignment.");
        if (current.TenantId != ev.TenantId || current.Scope != ev.Scope)
            throw new InvalidOperationException("A responsibility revocation has a different tenant or scope.");
        if (current.RevokedAt is { } revokedAt)
        {
            if (revokedAt == ev.RevokedAt && current.RevokedByMemberId == ev.RevokedByMemberId &&
                StringComparer.Ordinal.Equals(current.RevocationReason, ev.Reason) &&
                StringComparer.Ordinal.Equals(current.RevokedByDisplay, ev.RevokedByDisplay))
                return;
            throw new InvalidOperationException("A responsibility revocation was replayed with different terms.");
        }

        var updated = current with
        {
            RevokedAt = ev.RevokedAt,
            RevokedByMemberId = ev.RevokedByMemberId,
            RevocationReason = ev.Reason,
            RevokedByDisplay = ev.RevokedByDisplay,
        };
        await MemberResponsibilityIndexSchema.Assignments.ReplaceAsync(Transaction, current, updated, ct)
            .ConfigureAwait(false);
        var member = await MemberResponsibilityIndexSchema.Members.GetAsync(Transaction,
            current.MemberId, ct).ConfigureAwait(false) ?? throw new InvalidOperationException(
            "The member responsibility index row is missing.");
        await MemberResponsibilityIndexSchema.Members.ReplaceAsync(Transaction, member,
            member with
            {
                Assignments = member.Assignments.Select(item => item.AssignmentId == updated.AssignmentId && item.Scope == updated.Scope
                ? updated : item).ToArray()
            }, ct).ConfigureAwait(false);
    }

    async ValueTask UpsertAssignmentAsync(ResponsibilityAssigned ev, CancellationToken ct)
    {
        var assignment = new ResponsibilityAssignmentView(ev.TenantId, ev.AssignmentId, ev.MemberId,
            ev.Type, ev.Scope, ev.AssignedAt, ev.AssignedByMemberId, ev.EffectiveFrom,
            ev.EffectiveUntil, null, Uuid.Empty, ev.SeparationOfDutiesWaiverIds, ev.AssignedByDisplay);
        var existing = await MemberResponsibilityIndexSchema.Assignments.GetAsync(Transaction,
            MemberResponsibilityIndexSchema.AssignmentKey(ev.Scope, ev.AssignmentId), ct).ConfigureAwait(false);
        if (existing is not null)
        {
            if (!Equivalent(existing, assignment))
                throw new InvalidOperationException("A responsibility assignment was replayed with different terms.");
            return;
        }
        await MemberResponsibilityIndexSchema.Assignments.InsertAsync(Transaction, assignment, ct)
            .ConfigureAwait(false);
        var member = await MemberResponsibilityIndexSchema.Members.GetAsync(Transaction, ev.MemberId, ct)
            .ConfigureAwait(false);
        if (member is null)
            await MemberResponsibilityIndexSchema.Members.InsertAsync(Transaction,
                new MemberResponsibilityAssignments(ev.TenantId, ev.MemberId, [assignment]), ct)
                .ConfigureAwait(false);
        else if (member.TenantId != ev.TenantId)
            throw new InvalidOperationException("A responsibility index member belongs to another tenant.");
        else
            await MemberResponsibilityIndexSchema.Members.ReplaceAsync(Transaction, member,
                member with { Assignments = [.. member.Assignments, assignment] }, ct).ConfigureAwait(false);
    }

    static bool Equivalent(ResponsibilityAssignmentView left, ResponsibilityAssignmentView right) =>
        left.TenantId == right.TenantId && left.AssignmentId == right.AssignmentId &&
        left.MemberId == right.MemberId && left.Type == right.Type && left.Scope == right.Scope &&
        left.AssignedAt == right.AssignedAt && left.AssignedByMemberId == right.AssignedByMemberId &&
        left.EffectiveFrom == right.EffectiveFrom && left.EffectiveUntil == right.EffectiveUntil &&
        left.SeparationOfDutiesWaiverIds.SequenceEqual(right.SeparationOfDutiesWaiverIds) &&
        StringComparer.Ordinal.Equals(left.AssignedByDisplay, right.AssignedByDisplay);
}
