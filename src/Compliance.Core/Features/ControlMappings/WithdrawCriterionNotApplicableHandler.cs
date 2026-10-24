using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     Restores an accepted not-applicable criterion to coverage while keeping its history. The withdrawal names the
///     exact accepted decision, so a withdrawal prepared against an older decision cannot remove a newer accepted
///     one, and it is checked against the revision of that criterion line only.
/// </summary>
public sealed class WithdrawCriterionNotApplicableHandler(NativeOperationExecutor operations,
    ApplicabilityReader applicability, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<WithdrawCriterionNotApplicable>
{
    const string CommandKind = "WithdrawCriterionNotApplicable";

    public async ValueTask<Result> HandleAsync(IRequestContext<WithdrawCriterionNotApplicable> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.DecisionId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A withdrawal requires a command id, a decision and a non-negative expected revision."));
        if (await applicability.LocateAsync(request.TenantId, request.ProgramId, request.DecisionId, ct)
                .ConfigureAwait(false) is not { } criterion)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The criterion applicability decision was not found."));

        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var member = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var line = await applicability.ReadLineAsync(request.TenantId, request.ProgramId, criterion.EditionId,
            criterion.CriterionIdentifier, request.CommandId, ct).ConfigureAwait(false);
        var provenance = RootOperation.Provenance(request.CommandId, CommandKind, line.Stream, member,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (line.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = line.PrepareWithdrawal(request.DecisionId, request.ExpectedRevision, request.Rationale);
            if (!prepared.IsSuccess)
                return prepared;
            (response, result) = await NativeContent.FreezeUnitAsync(content, request.TenantId, ct)
                .ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<CriterionApplicability>(binding,
            () => new CriterionApplicability(request.TenantId, request.ProgramId, criterion.EditionId,
                criterion.CriterionIdentifier, request.CommandId), owner => owner.Acceptance,
            owner => Prepare(owner, request, response),
            (CriterionApplicability owner, ImmutableContentRef frozen) => owner.Withdraw(provenance,
                request.DecisionId, request.Rationale, member, now, frozen, result!)), context, ct)
            .ConfigureAwait(false);
        return NativeContent.UnitOutcome(outcome);
    }

    static Result<FirstResponseDescriptor> Prepare(CriterionApplicability owner, WithdrawCriterionNotApplicable request,
        FirstResponseDescriptor? response)
    {
        var prepared = owner.PrepareWithdrawal(request.DecisionId, request.ExpectedRevision, request.Rationale);
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return response is null
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The applicability line changed while the withdrawal was prepared; reload and retry.", true))
            : Result<FirstResponseDescriptor>.Success(response);
    }
}
