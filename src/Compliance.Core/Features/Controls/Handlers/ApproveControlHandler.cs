using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Controls.Commands;
using Bdgrz.Compliance.Features.Controls.Queries;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
namespace Bdgrz.Compliance.Features.Controls.Handlers;

public sealed class ApproveControlHandler(ControlOperations operations, IAggregateReader reader,
    IControlApplicabilityReferenceValidator applicability, ControlActivationReleaseGate releaseGate,
    ControlLifecycleReleaseGate lifecycleGate, ControlImpactService impact) : IRequestHandler<ApproveControl>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ApproveControl> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Control decision requires a personal HTTP invocation.")));
        var request = context.Request;
        var verifiedOwners = new HashSet<Uuid>();
        ControlPersonOwner? verifiedPerson = null;
        SeparationOfDutiesWaiverProof? waiver = null;
        return operations.ExecuteUnitAsync(context, request.TenantId, request.ProgramId, request.ControlId,
            request.CommandId, nameof(ApproveControl), request.ExpectedRevision,
            async (snapshot, actor, at, token) =>
            {
                if (!releaseGate.IsEnabled)
                    return Result.Failure(ControlActivationReleaseGate.Unavailable);
                if (snapshot.IsApproved)
                {
                    if (!lifecycleGate.IsEnabled)
                        return Result.Failure(ControlLifecycleReleaseGate.Unavailable);
                    var confirmed = await impact.ConfirmAsync(new PreviewControlImpact(request.TenantId,
                        request.ProgramId, request.ControlId, request.ExpectedRevision), request.ImpactDigest, token)
                        .ConfigureAwait(false);
                    if (!confirmed.IsSuccess)
                        return confirmed;
                }
                var valid = await applicability.ValidateAsync(request.TenantId, snapshot.CurrentContent, token).ConfigureAwait(false);
                if (!valid.IsSuccess)
                    return valid;
                verifiedOwners.Clear();
                foreach (var ownerId in snapshot.CurrentOwnerMemberIds(at))
                {
                    var member = await reader.HydrateAsync(Member.ForVerification(request.TenantId, ownerId), token).ConfigureAwait(false);
                    if (member.IsRegistered && !member.IsSuspended && member.Affiliation == "client_personnel")
                        verifiedOwners.Add(ownerId);
                }
                verifiedPerson = null;
                if (snapshot.CurrentPersonOwner is { } designated)
                {
                    var person = await reader.HydrateAsync(new Person(request.TenantId, designated.PersonId), token).ConfigureAwait(false);
                    if (person.IsCreated)
                        verifiedPerson = new ControlPersonOwner(designated.PersonId,
                            person.CorrelatedUserId is { } correlated ? RbacIds.Member(request.TenantId, correlated) : null,
                            designated.DesignationId);
                }
                var proof = await operations.ResolveWaiverAsync(request.TenantId, request.SeparationOfDutiesWaiverId,
                    actor.MemberId, request.ControlId, snapshot.DraftVersionId, request.ExpectedRevision,
                    SeparationOfDutiesActions.Approve, at, token).ConfigureAwait(false);
                waiver = proof.IsSuccess ? proof.Value : null;
                return proof.IsSuccess ? Result.Success : Result.Failure(proof.Error!);
            },
            (rules, actor, at) => ControlOperations.Outcome(rules.Approve(request.ProgramId, request.ExpectedRevision,
                request.CommandId, request.AcceptedReviewDecisionId, request.EffectiveFrom, request.Rationale,
                verifiedOwners, actor.MemberId, actor.Display, at, waiver, request.ImpactDigest, verifiedPerson)),
            null, () => waiver is null ? [] : [waiver], ct);
    }
}
