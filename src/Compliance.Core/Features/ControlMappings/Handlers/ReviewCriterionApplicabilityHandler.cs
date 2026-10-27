using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     The reviewer's personal decision on the pending not-applicable proposal of one criterion line; HTTP-only. The
///     proposal id is resolved to its criterion from the committed proposal facts, the decision is checked against the
///     exact revision of that line only, and an exact retry returns the original result even after later changes.
/// </summary>
public sealed class ReviewCriterionApplicabilityHandler(NativeOperationExecutor operations,
    ApplicabilityReader applicability, IAggregateReader reader, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<ReviewCriterionApplicability>
{
    const string CommandKind = "ReviewCriterionApplicability";

    public ValueTask<Result> HandleAsync(IRequestContext<ReviewCriterionApplicability> context,
        CancellationToken ct) =>
        MappingLineFailures.ExecuteAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result> HandleCoreAsync(IRequestContext<ReviewCriterionApplicability> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Criterion sign-off requires personal HTTP submission."));
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.DecisionId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An applicability review requires a command id, a decision and a non-negative expected revision."));
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

        MappingLinePrepared? prepared = null;
        var outcome = await operations.ExecuteAsync(new NativeOperation<CriterionApplicability>(binding,
            () => new CriterionApplicability(request.TenantId, request.ProgramId, criterion.EditionId, criterion.CriterionIdentifier, request.CommandId), owner => owner.Acceptance,
            async (owner, token) =>
            {
                var rules = await applicability.ComposeAsync(owner, token).ConfigureAwait(false);
                FirstResponseDescriptor? response = null;
                ImmutableContentRef? result = null;
                SeparationOfDutiesWaiver? waiver = null;
                if (request.Outcome == "accept" && rules.View is { Status: "pending" })
                {
                    var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId, request.ProgramId), token)
                        .ConfigureAwait(false);
                    if (program.CriteriaEditionId != criterion.EditionId)
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The decision targets an edition the program no longer selects."));
                }

                if (request.SeparationOfDutiesWaiverId is { } waiverId)
                    waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId, waiverId), token)
                        .ConfigureAwait(false);
                var guard = rules.PrepareReview(request.DecisionId, request.ExpectedRevision, request.Outcome,
                    request.Rationale, actor.MemberId, now, waiver);
                if (!guard.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(guard.Error!);
                (response, result) = await NativeContent.FreezeUnitAsync(content, request.TenantId, token)
                    .ConfigureAwait(false);

                var applied = rules.Review(provenance, request.DecisionId, request.Outcome, request.Rationale, actor.MemberId, member, now, waiver);
                if (!applied.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(applied.Error!);
                prepared = await MappingLineContent.PrepareAsync(content, owner, request, rules.Changes[^1],
                    MappingLineContent.Current(rules), MappingLineContent.Header(rules), result!, token).ConfigureAwait(false);
                return Result<FirstResponseDescriptor>.Success(response!);
            }, (owner, frozen) => owner.Apply(prepared ?? throw new InvalidOperationException("The native line was not prepared."), frozen)),
            context, ct).ConfigureAwait(false);
        return NativeContent.UnitOutcome(outcome);
    }
}
