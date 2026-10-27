using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NamedRecords.Aggregates;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NamedRecords;

/// <summary>
///     The four receivers every name-change process shares, written once over a <see cref="NamedRecordKindDescriptor" />:
///     claim a name, apply the accepted change, release a name and cancel a pending change. Each verifies the source
///     proof and the frozen intent against the command, derives its operation provenance from them, and accepts one
///     fact on its target owner with replay, supersession and rejection handled by <see cref="ReceiverAppend" />.
/// </summary>
public sealed class NamedRecordReceivers(ISourceFactResolver resolver, IImmutableContentStore content,
    ReceiverAppend append, IAggregateReader reader, TimeProvider clock)
{
    /// <summary>Verifies a source proof and returns the facts it proves, or a permanent conflict.</summary>
    public async Task<Result<VerifiedFact>> VerifyAsync<TFact>(SourceFactProof<TFact> proof, CancellationToken ct)
        where TFact : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(proof);
        try
        {
            var verified = await resolver.VerifyAsync(proof, ct).ConfigureAwait(false);
            return Result<VerifiedFact>.Success(new VerifiedFact(verified.EventId, verified.Source, verified.Facts));
        }
        catch (SourceFactVerificationException exception)
        {
            return Result<VerifiedFact>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The source proof does not verify: {exception.Message}", false));
        }
    }

    /// <summary>Acquires or re-pins the claim <paramref name="step" /> targets for the record's pending change.</summary>
    public async ValueTask<Result<ReceiverOutcome>> ClaimAsync<TClaim>(NamedRecordKindDescriptor kind,
        NameChangeStep step, string nameKind, string normalizedName, VerifiedFact source,
        Func<TClaim, ClaimAttempt, Result> claim, IExecutionContext context, CancellationToken ct)
        where TClaim : NameClaim
    {
        var checkedStep = await CheckAsync(kind, step, source, step.Target.Realm, ct).ConfigureAwait(false);
        if (!checkedStep.IsSuccess)
            return Result<ReceiverOutcome>.Failure(checkedStep.Error!);
        var (tenantId, recordStream, intent) = checkedStep.Value!;
        if (source.Fact is not INamedRecordFact { Operation.LogicalId: var logical } || logical != step.OperationId)
            return ReceiverAppend.Invalid("The source is not the accepted change of this operation.");
        var isNew = step.Target == intent.NewClaimKey;
        if (!isNew && step.Target != intent.OldClaimKey)
            return ReceiverAppend.Invalid("The target is not a claim this change needs.");
        if (kind.ClaimStream(tenantId, nameKind, normalizedName) != step.Target)
            return ReceiverAppend.Invalid("The command names a different claim than its target.");
        if (isNew)
        {
            var authored = await ReadNameAsync(kind, intent, step.Target.Realm, ct).ConfigureAwait(false);
            if (!authored.IsSuccess)
                return Result<ReceiverOutcome>.Failure(authored.Error!);
            if (authored.Value!.NormalizedName != normalizedName || authored.Value.NameKind != nameKind)
                return ReceiverAppend.Invalid("The command names a different name than the authored content.");
        }

        var provenance = Provenance(kind.ClaimCommandKind, step, source.EventId, step.Target, intent);
        return await append.ApplyAsync(() => (TClaim)kind.OpenClaim(tenantId, step.Target, step.CommandId),
            owner => owner.Acceptance, AcceptedOperationBinding.Of(provenance),
            owner => owner.EvaluateClaim(recordStream, step.Generation) switch
            {
                ClaimVerdict.Taken => ReceiverAppend.Rejected(
                    $"The {kind.Noun} name is already claimed by another {kind.Noun}."),
                ClaimVerdict.Superseded => ReceiverAppend.Superseded("A later change of this record pinned the name."),
                _ => null,
            },
            owner => claim(owner, new ClaimAttempt(provenance, recordStream, step.ExpectedRevision ?? 0,
                step.Generation, step.Intent)),
            context, ct).ConfigureAwait(false);
    }

    /// <summary>Applies the accepted change once every claim it needs is held by the record at this generation.</summary>
    public async ValueTask<Result<ReceiverOutcome>> ApplyAsync<TRecord>(NamedRecordKindDescriptor kind,
        NameChangeStep step, VerifiedFact source, Func<TRecord, ApplyAttempt, Result> apply,
        IExecutionContext context, CancellationToken ct) where TRecord : NamedRecord
    {
        var checkedStep = await CheckAsync(kind, step, source, step.Target.Realm, ct).ConfigureAwait(false);
        if (!checkedStep.IsSuccess)
            return Result<ReceiverOutcome>.Failure(checkedStep.Error!);
        var (tenantId, recordStream, intent) = checkedStep.Value!;
        if (step.Target != recordStream)
            return ReceiverAppend.Invalid("The command does not target the record of this change.");
        var provenance = Provenance(kind.ApplyCommandKind, step, source.EventId, recordStream, intent);
        var binding = AcceptedOperationBinding.Of(provenance);
        var realm = step.Target.Realm;

        // A replay of an applied step must not look at claims again: the old claim may be released by now.
        var decided = await reader.HydrateAsync(kind.OpenRecord(tenantId, step.RecordId, step.CommandId), ct)
            .ConfigureAwait(false);
        ImmutableContentRef? contentRef = null;
        ImmutableContentRef? resultRef = null;
        var create = intent.ExpectedRecordRevision == 0;
        if (decided.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            foreach (var key in ClaimKeys(intent))
            {
                var claim = await reader.HydrateAsync(kind.OpenClaim(tenantId, key, Uuid.Empty), ct)
                    .ConfigureAwait(false);
                if (!claim.IsHeldBy(recordStream, step.Generation))
                    return ReceiverAppend.Invalid("A name claim the change needs is not held by this change.");
            }

            try
            {
                var bytes = await content.ReadVerifiedAsync(realm, intent.AuthoredContent, ct).ConfigureAwait(false);
                var schema = create ? kind.CreatedContentSchema : kind.RevisedContentSchema;
                contentRef = await content.WriteVerifiedAsync(realm, Uuid.CreateVersion5(step.CommandId, schema),
                    schema, ImmutableContentStoreExtensions.JsonMediaType, bytes.ToArray(), ct).ConfigureAwait(false);
                var revision = (long)intent.ExpectedRecordRevision + 1;
                var (resultSchema, resultBytes) = kind.ResultContent(create, step.RecordId, revision);
                var resultContent = await content.WriteVerifiedAsync(realm,
                    Uuid.CreateVersion5(step.CommandId, resultSchema), resultSchema,
                    ImmutableContentStoreExtensions.JsonMediaType, resultBytes, ct).ConfigureAwait(false);
                resultRef = await content.WriteJsonAsync(realm,
                    Uuid.CreateVersion5(step.CommandId, "mutation-result-content.v1"), "mutation-result-content.v1",
                    new MutationResultContent(resultSchema, new StandardResponseBody(resultContent)), ct)
                    .ConfigureAwait(false);
            }
            catch (ImmutableContentIntegrityException exception)
            {
                return ReceiverAppend.Invalid($"The frozen content does not verify: {exception.Message}");
            }
        }

        return await append.ApplyAsync(() => (TRecord)kind.OpenRecord(tenantId, step.RecordId, step.CommandId),
            record => record.Acceptance, binding,
            record => record.IsPending(step.OperationId, step.Generation)
                ? null
                : ReceiverAppend.Superseded("The change is no longer pending."),
            record => apply(record, new ApplyAttempt(provenance, step.Intent, contentRef!,
                intent.Header.OriginalActor, intent.Header.AcceptedAt, resultRef!)), context, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Releases a claim once the record's own fact proves it is no longer needed: an applied change lets go of the
    ///     old claim, a canceled change gives back the new claim. Only the exact holder and generation releases.
    /// </summary>
    public async ValueTask<Result<ReceiverOutcome>> ReleaseAsync<TClaim>(NamedRecordKindDescriptor kind,
        NameChangeStep step, string normalizedName, VerifiedFact source, Func<TClaim, ClaimAttempt, Result> release,
        IExecutionContext context, CancellationToken ct) where TClaim : NameClaim
    {
        var checkedStep = await CheckAsync(kind, step, source, step.Target.Realm, ct).ConfigureAwait(false);
        if (!checkedStep.IsSuccess)
            return Result<ReceiverOutcome>.Failure(checkedStep.Error!);
        var (tenantId, recordStream, intent) = checkedStep.Value!;
        var canceled = source.Fact is INamedRecordFact { Operation.CommandKind: var commandKind } &&
                       commandKind == kind.CancelCommandKind;
        if (!canceled && source.Fact is not INamedRecordAppliedFact)
            return ReceiverAppend.Invalid("The source fact proves neither an applied nor a canceled change.");
        var releasable = canceled ? NewOnly(intent) : OldOnly(intent);
        if (releasable is null || step.Target != releasable)
            return ReceiverAppend.Invalid("The target is not a claim this proof lets go of.");
        if (string.IsNullOrEmpty(normalizedName))
            return ReceiverAppend.Invalid("A release names the normalized name it lets go of.");
        var provenance = Provenance(kind.ReleaseCommandKind, step, source.EventId, step.Target, intent);
        return await append.ApplyAsync(() => (TClaim)kind.OpenClaim(tenantId, step.Target, step.CommandId),
            owner => owner.Acceptance, AcceptedOperationBinding.Of(provenance),
            owner => owner.IsHeldBy(recordStream, step.Generation)
                ? null
                : ReceiverAppend.Superseded("The claim is not held by this change; a later holder or generation owns it."),
            owner => release(owner, new ClaimAttempt(provenance, recordStream, step.ExpectedRevision ?? 0,
                step.Generation, step.Intent)), context, ct).ConfigureAwait(false);
    }

    /// <summary>Cancels the exact pending change after a definite failure and fences its local apply.</summary>
    public async ValueTask<Result<ReceiverOutcome>> CancelAsync<TRecord>(NamedRecordKindDescriptor kind,
        Uuid commandId, Uuid tenantId, Uuid recordId, Uuid changeOperationId, long expectedRevision,
        ImmutableContentRef failureProof, Func<TRecord, CancelAttempt, Result> cancel, IExecutionContext context,
        CancellationToken ct) where TRecord : NamedRecord
    {
        if (tenantId == Uuid.Empty || recordId == Uuid.Empty || changeOperationId == Uuid.Empty)
            return ReceiverAppend.Invalid("A cancellation names its tenant, record and operation.");
        var realm = tenantId.ToString();
        NameChangeFailureProof proof;
        try
        {
            proof = await content.ReadJsonAsync<NameChangeFailureProof>(realm, failureProof, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return ReceiverAppend.Invalid($"The failure proof does not verify: {exception.Message}");
        }

        if (proof.OperationId != changeOperationId)
            return ReceiverAppend.Invalid("The failure proof names another operation.");
        var stream = kind.OpenRecord(tenantId, recordId, Uuid.Empty).Stream;
        var provenance = new OperationProvenance(commandId, changeOperationId, null, kind.CancelCommandKind,
            ActorReference.ForSystemProcess(kind.ProcessName, kind.ProcessName), stream, "current",
            expectedRevision >= 0 ? (ulong)expectedRevision : null, failureProof.Sha256, clock.GetUtcNow(), stream,
            stream);
        return await append.ApplyAsync(() => (TRecord)kind.OpenRecord(tenantId, recordId, commandId),
            record => record.Acceptance, AcceptedOperationBinding.Of(provenance),
            record => record.Pending is { } pending && pending.RootOperationId == changeOperationId &&
                      pending.Generation == proof.Generation && pending.ExpectedRevision == expectedRevision
                ? null
                : ReceiverAppend.Superseded("The change is already applied or canceled; it recovers forward."),
            record => cancel(record, new CancelAttempt(provenance, failureProof)), context, ct)
            .ConfigureAwait(false);
    }

    /// <summary>The distinct claim keys a change needs, in canonical unsigned UTF-8 order.</summary>
    public static IReadOnlyList<EventStreamAddress> ClaimKeys(RecordNameChangeIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        return new[] { intent.OldClaimKey, intent.NewClaimKey }.OfType<EventStreamAddress>()
            .DistinctBy(static key => key.Resource).OrderBy(static key => key.Resource, StreamKeyOrder.Instance)
            .ToArray();
    }

    /// <summary>The claim the change acquires and a cancellation must give back; null when it only keeps its old claim.</summary>
    public static EventStreamAddress? NewOnly(RecordNameChangeIntent intent) =>
        intent.NewClaimKey is { } key && key != intent.OldClaimKey ? key : null;

    /// <summary>The claim an applied change lets go of; null when the record keeps the same claim or held none.</summary>
    public static EventStreamAddress? OldOnly(RecordNameChangeIntent intent) =>
        intent.OldClaimKey is { } key && key != intent.NewClaimKey ? key : null;

    /// <summary>The name the authored content of the intent claims.</summary>
    public async Task<Result<NamedContentName>> ReadNameAsync(NamedRecordKindDescriptor kind,
        RecordNameChangeIntent intent, string realm, CancellationToken ct)
    {
        try
        {
            var bytes = await content.ReadVerifiedAsync(realm, intent.AuthoredContent, ct).ConfigureAwait(false);
            return Result<NamedContentName>.Success(kind.ReadName(bytes));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<NamedContentName>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The authored content does not verify: {exception.Message}", false));
        }
    }

    async Task<Result<(Uuid TenantId, EventStreamAddress RecordStream, RecordNameChangeIntent Intent)>> CheckAsync(
        NamedRecordKindDescriptor kind, NameChangeStep step, VerifiedFact source, string realm, CancellationToken ct)
    {
        if (!Uuid.TryParse(realm, CultureInfo.InvariantCulture, out var tenantId) || tenantId == Uuid.Empty)
            return Fail("The target is not in a tenant realm.");
        var recordStream = kind.OpenRecord(tenantId, step.RecordId, Uuid.Empty).Stream;
        var proven = source.Fact switch
        {
            INamedRecordFact named => named.Operation.RootOperationId == step.OperationId &&
                                      named.Owner == recordStream && named.Generation == step.Generation &&
                                      named.Intent == step.Intent,
            INamedRecordAppliedFact applied => applied.Operation.RootOperationId == step.OperationId &&
                                               source.Stream == recordStream && applied.Intent == step.Intent,
            _ => false,
        };
        if (source.Stream.Realm != realm || !proven)
            return Fail("The source fact does not carry this operation, generation and intent.");
        RecordNameChangeIntent intent;
        try
        {
            intent = await content.ReadJsonAsync<RecordNameChangeIntent>(realm, step.Intent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Fail($"The frozen intent does not verify: {exception.Message}");
        }

        return intent.RecordKind != kind.Kind || intent.RecordId != step.RecordId ||
               intent.Header.RootOperationId != step.OperationId ||
               intent.CanonicalNormalizationVersion != RecordNameChangeIntent.NormalizationVersion ||
               intent.Header.FirstOwner != recordStream
            ? Fail("The intent names another record, operation or normalization.")
            : Result<(Uuid, EventStreamAddress, RecordNameChangeIntent)>.Success((tenantId, recordStream, intent));

        static Result<(Uuid TenantId, EventStreamAddress RecordStream, RecordNameChangeIntent Intent)> Fail(
            string message) =>
            Result<(Uuid, EventStreamAddress, RecordNameChangeIntent)>.Failure(
                new RequestError(RequestErrorKind.Conflict, message, false));
    }

    static OperationProvenance Provenance(string commandKind, NameChangeStep step, Uuid causation,
        EventStreamAddress owner, RecordNameChangeIntent intent) =>
        new(step.CommandId, step.OperationId, causation, commandKind, intent.Header.OriginalActor, owner,
            intent.Header.Episode, step.ExpectedRevision is >= 0 and var revision ? (ulong)revision : null,
            intent.Header.AuthoredIntentSha256, intent.Header.AcceptedAt, owner, owner);
}

/// <summary>The fields every receiver command of a name-change process carries.</summary>
public sealed record NameChangeStep(Uuid CommandId, EventStreamAddress Target, Uuid OperationId, long Generation,
    long? ExpectedRevision, ImmutableContentRef Intent, Uuid RecordId);

/// <summary>A source fact whose proof was re-read and matched.</summary>
public sealed record VerifiedFact(Uuid EventId, EventStreamAddress Stream, DomainEvent Fact);

/// <summary>What a typed claim owner needs to claim or release a name for one change.</summary>
public sealed record ClaimAttempt(OperationProvenance Operation, EventStreamAddress Holder, long ExpectedRevision,
    long Generation, ImmutableContentRef Intent);

/// <summary>What a typed record owner needs to apply a verified change.</summary>
public sealed record ApplyAttempt(OperationProvenance Operation, ImmutableContentRef Intent,
    ImmutableContentRef Content, ActorReference Actor, DateTimeOffset ChangedAt, ImmutableContentRef Result);

/// <summary>What a typed record owner needs to cancel its pending change.</summary>
public sealed record CancelAttempt(OperationProvenance Operation, ImmutableContentRef FailureProof);
