using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Responsibilities.Commands;
using Bdgrz.Compliance.Features.Responsibilities.Events;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Readers;
namespace Bdgrz.Compliance.Features.Commitments.Services;

public sealed partial class CommitmentOperations
{
    public ValueTask<Result> AssignResponsibilityAsync(IRequestContext<AssignResponsibility> context,
        ITenantMembershipDirectoryReader memberships, IResponsibilityScopeValidator scopes, CancellationToken ct)
    {
        var request = context.Request;
        var proofs = new List<SeparationOfDutiesWaiverProof>();
        var memberId = Uuid.Empty;
        return ExecuteUnitAsync(context, request.TenantId, null, request.RecordId, request.CommandId,
            nameof(AssignResponsibility), request.ScopeRevision,
            async (snapshot, actor, at, token) =>
            {
                if (!snapshot.IsCurrentResponsibilityScope(request.Scope))
                    return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        "Responsibilities must target the current exact pending commitment revision."));
                var valid = await scopes.ValidateAsync(request.TenantId, request.Scope, token).ConfigureAwait(false);
                if (!valid.IsSuccess)
                    return valid;
                var member = await memberships.GetAsync(request.TenantId.ToString(), request.MemberUserId, token)
                    .ConfigureAwait(false);
                if (member is null || member.IsSuspended || member.Affiliation == "firm_staff")
                    return Result.Failure(new RequestError(RequestErrorKind.Validation,
                        "The responsibility assignee must be an active tenant member."));
                memberId = RbacIds.Member(request.TenantId, request.MemberUserId);
                var proposed = new ResponsibilityAssignmentView(request.TenantId, request.CommandId, memberId,
                    request.Type, request.Scope, at, actor.MemberId, request.EffectiveFrom, request.EffectiveUntil,
                    null, Uuid.Empty, []);
                var actions = ResponsibilityConflictPolicy.FindConflicts(snapshot.GetResponsibilitySet(request.Scope)
                    .ReadAssignments(), proposed).Select(value => value.WaiverAction).Distinct(StringComparer.Ordinal).ToArray();
                proofs.Clear();
                if (actions.Length == 0 && request.SeparationOfDutiesWaiverIds is { Count: > 0 })
                    return Result.Failure(new RequestError(RequestErrorKind.Validation,
                        "A responsibility assignment can reference only waivers that authorize its current conflicts."));
                foreach (var id in request.SeparationOfDutiesWaiverIds ?? [])
                {
                    SeparationOfDutiesWaiverProof? accepted = null;
                    foreach (var action in actions)
                    {
                        var proof = await ResolveWaiverAsync(request.TenantId, id, memberId, request.RecordId,
                            request.VersionId, request.ScopeRevision, action, at, token).ConfigureAwait(false);
                        if (proof.IsSuccess)
                        {
                            accepted = proof.Value;
                            break;
                        }
                        if (proof.Error!.IsTransient)
                            return Result.Failure(proof.Error);
                    }
                    if (accepted is null)
                        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The responsibility requires an approved, active exact-scope waiver."));
                    proofs.Add(accepted);
                }
                // Preserve the source membership recheck after proof preparation.
                var current = await Sources.HydrateAsync(new Member(request.TenantId, request.MemberUserId), token)
                    .ConfigureAwait(false);
                return current.IsRegistered && !current.IsSuspended && current.Affiliation != "firm_staff"
                    ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Validation,
                        "The responsibility assignee must be an active tenant member."));
            },
            (rules, actor, at) => Outcome(rules.AssignResponsibility(request.Scope, request.CommandId,
                memberId, request.Type, actor.MemberId, actor.Display, at, request.EffectiveFrom,
                request.EffectiveUntil, proofs)), null, () => proofs, ct);
    }

    public ValueTask<Result> RevokeResponsibilityAsync(IRequestContext<RevokeResponsibility> context,
        IResponsibilityScopeValidator scopes, CancellationToken ct)
    {
        var request = context.Request;
        return ExecuteUnitAsync(context, request.TenantId, null, request.RecordId, request.CommandId,
            nameof(RevokeResponsibility), request.ScopeRevision,
            async (snapshot, actor, at, token) => !snapshot.IsCurrentResponsibilityScope(request.Scope)
                ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "Responsibilities must target the current exact pending commitment revision."))
                : await scopes.ValidateAsync(request.TenantId, request.Scope, token).ConfigureAwait(false),
            (rules, actor, at) => Outcome(rules.RevokeResponsibility(request.Scope, request.AssignmentId,
                actor.MemberId, actor.Display, at, request.Reason)),
            (rules, actor, at) => new ResponsibilityRevoked(request.TenantId, request.AssignmentId,
                request.Scope, actor.MemberId, at, request.Reason.Trim(), actor.Display)
            { StoredActor = actor.Snapshot }, null, ct);
    }
}
