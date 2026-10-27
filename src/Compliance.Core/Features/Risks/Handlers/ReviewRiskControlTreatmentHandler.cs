using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Independence;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks.Handlers;

/// <summary>Records exact independent review; current personal authorization precedes native retry resolution.</summary>
public sealed class ReviewRiskControlTreatmentHandler(NativeOperationExecutor operations, IAggregateReader reader,
    RiskGovernanceReader governanceReader, ISeparationOfDutiesWaiverProofResolver waivers,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<ReviewRiskControlTreatment>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviewRiskControlTreatment> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Risk sign-off requires a personal HTTP invocation."));
        var request = context.Request;
        var reviewed = await RiskTreatmentAssertionOperations.ExecuteAsync<ReviewRiskControlTreatment, UnitResult>(operations, reader,
            governanceReader, content, clock, context, request.TenantId, request.ProgramId, request.RiskId, request.CommandId,
            request.ExpectedRevision, null, request.TreatmentId, nameof(ReviewRiskControlTreatment),
            async (owner, operation, token) =>
            {
                if (owner.Pending is not { } pending || pending.TreatmentId != request.TreatmentId)
                    return Failure(RequestErrorKind.Conflict, "The control treatment has no pending assertion to review.");
                var gate = owner.PrepareReview(request.TreatmentId, request.ExpectedRevision);
                if (!gate.IsSuccess)
                    return Result<PreparedRiskAssertion>.Failure(gate.Error!);
                var proposed = await FindingContent.ReadAsync<RiskControlTreatmentView>(content, request.TenantId, pending.Proposed.TreatmentContent, token).ConfigureAwait(false);
                var member = Uuid.Parse(operation.Actor.Id, System.Globalization.CultureInfo.InvariantCulture);
                if (request.SeparationOfDutiesWaiverId is { } waiver)
                {
                    var scope = new SeparationOfDutiesWaiverProofScope(request.TenantId, waiver, member, member,
                        SeparationOfDutiesActions.Review, SeparationOfDutiesRecordTypes.RiskControlTreatment, request.TreatmentId,
                        proposed.ControlVersionId, request.ExpectedRevision);
                    var proof = await waivers.ResolveAsync(scope, operation.AcceptedAt, token).ConfigureAwait(false);
                    if (!proof.IsSuccess)
                        return Failure(RequestErrorKind.Forbidden, "The separation-of-duties waiver is not active for this member and treatment revision.");
                }
                else if (pending.ProposerMemberId == member)
                    return Failure(RequestErrorKind.Forbidden, "A control treatment proposer cannot review their own assertion.");
                if (request.Outcome is not (RiskStates.Accept or RiskStates.Reject) || RiskCommands.InvalidText(request.Rationale))
                    return Failure(RequestErrorKind.Validation, "A treatment review requires accept or reject and a bounded rationale.");
                var frozen = await RiskCommands.FreezeUnitAsync(content, request.TenantId, token).ConfigureAwait(false);
                return Result<PreparedRiskAssertion>.Success(new(frozen.Response, frozen.Result));
            }, (owner, operation, first, frozen) => owner.Review(operation, request.TreatmentId, request.Outcome,
                request.Rationale.Trim(), operation.Actor, operation.AcceptedAt, request.SeparationOfDutiesWaiverId, first, frozen.Result), ct).ConfigureAwait(false);
        return reviewed.IsSuccess ? Result.Success : Result.Failure(reviewed.Error!);
    }

    static Result<PreparedRiskAssertion> Failure(RequestErrorKind kind, string message) => Result<PreparedRiskAssertion>.Failure(new RequestError(kind, message));
}
