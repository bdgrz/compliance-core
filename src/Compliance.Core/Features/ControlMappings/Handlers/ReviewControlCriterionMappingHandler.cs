using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.ControlMappings.Aggregates;
using Bdgrz.Compliance.Features.ControlMappings.Commands;
using Bdgrz.Compliance.Features.ControlMappings.Readers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings.Handlers;

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

    public ValueTask<Result> HandleAsync(IRequestContext<ReviewControlCriterionMapping> context,
        CancellationToken ct) =>
        MappingLineFailures.ExecuteAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result> HandleCoreAsync(IRequestContext<ReviewControlCriterionMapping> context,
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

        MappingLinePrepared? prepared = null;
        var outcome = await operations.ExecuteAsync(new NativeOperation<ControlCriterionMapping>(binding,
            () => new ControlCriterionMapping(request.TenantId, request.ProgramId, tuple.ControlId, tuple.EditionId, tuple.CriterionIdentifier, request.CommandId), owner => owner.Acceptance,
            async (owner, token) =>
            {
                var rules = await mappings.ComposeAsync(owner, token).ConfigureAwait(false);
                FirstResponseDescriptor? response = null;
                ImmutableContentRef? result = null;
                SeparationOfDutiesWaiver? waiver = null;
                if (request.Outcome == "accept" && rules.View is { Status: "pending" })
                {
                    var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId, request.ProgramId), token)
                        .ConfigureAwait(false);
                    if (program.CriteriaEditionId != tuple.EditionId)
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The mapping targets an edition the program no longer selects; remap it explicitly."));
                }

                if (request.SeparationOfDutiesWaiverId is { } waiverId)
                    waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId, waiverId), token)
                        .ConfigureAwait(false);
                var guard = rules.PrepareReview(request.MappingId, request.ExpectedRevision, request.Outcome,
                    request.Rationale, actor.MemberId, now, waiver);
                if (!guard.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(guard.Error!);
                (response, result) = await NativeContent.FreezeUnitAsync(content, request.TenantId, token)
                    .ConfigureAwait(false);

                var applied = rules.Review(provenance, request.MappingId, request.Outcome, request.Rationale, actor.MemberId, actor.Display, now, waiver);
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
