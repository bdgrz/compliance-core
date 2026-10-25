using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Full local reconciliation, actual close/source preparation and exact native original Unit admission.</summary>
public sealed class AuditPopulationMutations(NativeOperationExecutor operations, IAggregateReader reader,
    IAggregateExecutor executor, IDomainEventReader events, IImmutableContentStore content, ISourceFactResolver resolver,
    TypeIIPeriodReader periods, AuditPopulationSources sources, TimeProvider clock)
{
    public async ValueTask<Result> ExecuteAsync<T>(IRequestContext<T> context, CancellationToken ct) where T : IRequest
    {
        try { return await ExecuteCoreAsync(context, ct).ConfigureAwait(false); }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result.Failure(Unverified(error));
        }
    }
    async ValueTask<Result> ExecuteCoreAsync<T>(IRequestContext<T> context, CancellationToken ct) where T : IRequest
    {
        // The actual DTO is copied before any await so caller-owned lists/dictionaries cannot change the admitted intent.
        var authored = AuditPopulationCommandSelection.Snapshot(context.Request);
        var selection = AuditPopulationCommandSelection.Of(authored);
        if (selection.CommandId == Uuid.Empty || selection.TenantId == Uuid.Empty || selection.ProgramId == Uuid.Empty ||
            selection.PopulationId == Uuid.Empty || selection.ExpectedRevision < 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A population decision requires a typed command and exact tenant, program, owner and original revision."));
        if (!AuditPopulationCommandActor.TryGet(context.Actor, context.Invocation, selection.TenantId, out var member, out var display))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "A population decision requires a personal HTTP actor."));
        var actor = ActorReference.ForMember(member, display.Trim());
        var ownerStream = new AuditPopulation(selection.TenantId, selection.ProgramId, selection.PopulationId).Stream;
        var operation = RootOperation.Provenance(selection.CommandId, selection.Kind, ownerStream, actor,
            selection.ExpectedRevision, selection.Digest, clock.GetUtcNow()) with { FirstOwner = ownerStream };
        Prepared? prepared = null;
        var accepted = await operations.ExecuteAsync(new NativeOperation<AuditPopulation>(AcceptedOperationBinding.Of(operation),
            () => new AuditPopulation(selection.TenantId, selection.ProgramId, selection.PopulationId, selection.CommandId),
            owner => owner.Acceptance, async (owner, cancel) =>
            {
                var cut = AuditPopulationPreparedCut.Of(owner);
                var complete = await AuditPopulationHistory.ReadAsync(events, content, resolver, owner, cancel).ConfigureAwait(false);
                if (complete.View is not { } before)
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound, "The audit population was not found."));
                var at = clock.GetUtcNow();
                var failure = AuditPopulationContent.Decide(complete.Rules, authored, actor, at);
                if (failure is not null)
                    return Result<FirstResponseDescriptor>.Failure(failure.ToRequestError());
                if (complete.Rules.PendingChanges.Count != 1 || complete.Rules.Current is not { } after)
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "A fresh typed command requires one complete accepted local population decision."));
                var source = await sources.FreezeAsync(owner, before, periods, cancel).ConfigureAwait(false);
                var body = await AuditPopulationContent.FreezeStateAsync(content, after, cancel).ConfigureAwait(false);
                if (!body.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(body.Error!);
                if (JsonSerializer.SerializeToUtf8Bytes(selection.Content, ComplianceCoreJsonContext.Default.AuditPopulationCommandContent).LongLength >
                    FitzKvImmutableContentStore.MaximumContentBytes)
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Validation,
                        "The complete authored population command exceeds the existing immutable-content byte admission bound."));
                var command = await NativeContent.WriteAsync(content, owner.TenantId, AuditPopulationCommandContent.Schema,
                    selection.Content, cancel).ConfigureAwait(false);
                var response = await NativeContent.FreezeUnitAsync(content, owner.TenantId, cancel).ConfigureAwait(false);
                prepared = new(cut, complete.Rules.PendingChanges[0], after, body.Value!, command, source,
                    operation with { AcceptedAt = at }, response.Result);
                return Result<FirstResponseDescriptor>.Success(response.Response);
            }, (owner, first) =>
            {
                var decision = prepared ?? throw new InvalidOperationException("The native population command has no exact prepared local decision.");
                return owner.Append(AuditPopulationContent.NativeFact(decision.Rule, decision.State, decision.Body,
                    decision.Command, decision.Sources, decision.Operation, first, decision.Result), decision.Cut);
            }), context, ct).ConfigureAwait(false);
        return NativeContent.UnitOutcome(accepted);
    }

    // These three existing producers retain their old unqualified retry semantics; this content adapter makes no native receipt claim.
    internal async ValueTask<Result<AuditPopulationView>> AppendUnqualifiedAsync<T>(IRequestContext<T> context,
        Uuid tenantId, Uuid programId, Uuid populationId, Func<AuditPopulationRules, CommandFailure?> decide, CancellationToken ct)
        where T : IRequestBase
    {
        try
        {
            var owner = await reader.HydrateAsync(new AuditPopulation(tenantId, programId, populationId), ct).ConfigureAwait(false);
            var cut = AuditPopulationPreparedCut.Of(owner);
            var complete = await AuditPopulationHistory.ReadAsync(events, content, resolver, owner, ct).ConfigureAwait(false);
            var failure = decide(complete.Rules);
            if (failure is not null)
                return Result<AuditPopulationView>.Failure(failure.ToRequestError());
            var state = complete.Rules.Current ?? throw new InvalidOperationException("An accepted population producer has no complete result.");
            if (complete.Rules.PendingChanges.Count == 0)
                return Result<AuditPopulationView>.Success(state);
            if (complete.Rules.PendingChanges.Count != 1)
                throw new InvalidOperationException("A population producer requires one exact local decision.");
            var body = await AuditPopulationContent.FreezeStateAsync(content, state, ct).ConfigureAwait(false);
            if (!body.IsSuccess)
                return Result<AuditPopulationView>.Failure(body.Error!);
            var fact = AuditPopulationContent.UnqualifiedFact(complete.Rules.PendingChanges[0], state, body.Value!);
            return await executor.ExecuteAsync(new AuditPopulation(tenantId, programId, populationId), current =>
            {
                var append = current.Append(fact, cut);
                return append.IsSuccess ? AggregateOutcome.Commit(Result<AuditPopulationView>.Success(state)) :
                    AggregateOutcome.Discard(Result<AuditPopulationView>.Failure(append.Error!));
            }, context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result<AuditPopulationView>.Failure(Unverified(error));
        }
    }
    static RequestError Unverified(ImmutableContentIntegrityException error) => new(RequestErrorKind.Conflict,
        "The exact population body, source or original response cannot be verified.", error is ImmutableContentUnavailableException);
    sealed record Prepared(AuditPopulationPreparedCut Cut, DomainEvent Rule, AuditPopulationView State, ImmutableContentRef Body,
        ImmutableContentRef Command, ImmutableContentRef Sources, OperationProvenance Operation, ImmutableContentRef Result);
}
