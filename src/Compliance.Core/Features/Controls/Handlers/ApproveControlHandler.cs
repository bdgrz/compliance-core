using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

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
