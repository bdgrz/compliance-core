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
///     The reviewer's personal decision on the pending proposal of one mapping line; HTTP-only. The proposal id is
///     resolved to its tuple from the committed proposal facts, the decision is checked against the exact revision of
///     that line only, and an exact retry returns the original result even after later reviews or proposals.
/// </summary>
public sealed class ReviewControlCriterionMappingHandler(NativeOperationExecutor operations,
    ControlMappingReader mappings, IAggregateReader reader, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<ReviewControlCriterionMapping>
{
    const string CommandKind = "ReviewControlCriterionMapping";

    public async ValueTask<Result> HandleAsync(IRequestContext<ReviewControlCriterionMapping> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Criterion sign-off requires personal HTTP submission."));
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.MappingId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A mapping review requires a command id, a mapping and a non-negative expected revision."));
        if (await mappings.LocateAsync(request.TenantId, request.ProgramId, request.MappingId, ct)
                .ConfigureAwait(false) is not { } tuple)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The control criterion mapping was not found."));

        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var now = clock.GetUtcNow();
        var line = await mappings.ReadLineAsync(request.TenantId, request.ProgramId, tuple.ControlId,
            tuple.EditionId, tuple.CriterionIdentifier, request.CommandId, ct).ConfigureAwait(false);
        var provenance = RootOperation.Provenance(request.CommandId, CommandKind, line.Stream,
            ActorReference.ForMember(actor.MemberId, actor.Display), request.ExpectedRevision,
            AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        SeparationOfDutiesWaiver? waiver = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (line.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            if (request.Outcome == "accept" && line.View is { Status: "pending" })
            {
                var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId, request.ProgramId), ct)
                    .ConfigureAwait(false);
                if (program.CriteriaEditionId != tuple.EditionId)
                    return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The mapping targets an edition the program no longer selects; remap it explicitly."));
            }

            if (request.SeparationOfDutiesWaiverId is { } waiverId)
                waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId, waiverId), ct)
                    .ConfigureAwait(false);
            var prepared = line.PrepareReview(request.MappingId, request.ExpectedRevision, request.Outcome,
                request.Rationale, actor.MemberId, now, waiver);
            if (!prepared.IsSuccess)
                return prepared;
            (response, result) = await NativeContent.FreezeUnitAsync(content, request.TenantId, ct)
                .ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<ControlCriterionMapping>(binding,
            () => new ControlCriterionMapping(request.TenantId, request.ProgramId, tuple.ControlId, tuple.EditionId,
                tuple.CriterionIdentifier, request.CommandId), owner => owner.Acceptance,
            owner => Prepare(owner, request, actor.MemberId, now, waiver, response),
            (ControlCriterionMapping owner, ImmutableContentRef frozen) => owner.Review(provenance, request.MappingId,
                request.Outcome, request.Rationale, actor.MemberId, actor.Display, now, waiver, frozen, result!)),
            context, ct).ConfigureAwait(false);
        return NativeContent.UnitOutcome(outcome);
    }

    static Result<FirstResponseDescriptor> Prepare(ControlCriterionMapping owner, ReviewControlCriterionMapping request,
        Uuid reviewerMemberId, DateTimeOffset now, SeparationOfDutiesWaiver? waiver, FirstResponseDescriptor? response)
    {
        var prepared = owner.PrepareReview(request.MappingId, request.ExpectedRevision, request.Outcome,
            request.Rationale, reviewerMemberId, now, waiver);
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return response is null
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The mapping line changed while the review was prepared; reload and retry.", true))
            : Result<FirstResponseDescriptor>.Success(response);
    }
}
