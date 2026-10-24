using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     Withdraws the accepted mapping from coverage while keeping its history. The retirement names the exact
///     accepted proposal, so a retirement prepared against an older mapping cannot remove a newer accepted
///     successor, and it is checked against the revision of that mapping line only.
/// </summary>
public sealed class RetireControlCriterionMappingHandler(NativeOperationExecutor operations,
    ControlMappingReader mappings, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<RetireControlCriterionMapping>
{
    const string CommandKind = "RetireControlCriterionMapping";

    public async ValueTask<Result> HandleAsync(IRequestContext<RetireControlCriterionMapping> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.MappingId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A mapping retirement requires a command id, a mapping and a non-negative expected revision."));
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

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (line.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = line.PrepareRetirement(request.MappingId, request.ExpectedRevision, request.Rationale);
            if (!prepared.IsSuccess)
                return prepared;
            (response, result) = await NativeContent.FreezeUnitAsync(content, request.TenantId, ct)
                .ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<ControlCriterionMapping>(binding,
            () => new ControlCriterionMapping(request.TenantId, request.ProgramId, tuple.ControlId, tuple.EditionId,
                tuple.CriterionIdentifier, request.CommandId), owner => owner.Acceptance,
            owner => Prepare(owner, request, response),
            (ControlCriterionMapping owner, ImmutableContentRef frozen) => owner.Retire(provenance, request.MappingId,
                request.Rationale, actor.MemberId, actor.Display, now, frozen, result!)), context, ct)
            .ConfigureAwait(false);
        return NativeContent.UnitOutcome(outcome);
    }

    static Result<FirstResponseDescriptor> Prepare(ControlCriterionMapping owner, RetireControlCriterionMapping request,
        FirstResponseDescriptor? response)
    {
        var prepared = owner.PrepareRetirement(request.MappingId, request.ExpectedRevision, request.Rationale);
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return response is null
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The mapping line changed while the retirement was prepared; reload and retry.", true))
            : Result<FirstResponseDescriptor>.Success(response);
    }
}
