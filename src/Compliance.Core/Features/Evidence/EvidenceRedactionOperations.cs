using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Current authorization, exact source preparation and native original-result admission for redaction decisions.</summary>
sealed class EvidenceRedactionOperations(EvidenceRedactionAccess access, EvidenceRedactionSources sources,
    EvidenceRedactionWaiverRead waivers, IAggregateReader reader, IDomainEventReader events, ISourceFactResolver resolver,
    NativeOperationExecutor operations, IImmutableContentStore content, EvidenceRedactionProofs proofs, TimeProvider clock)
{
    public const string ResultSchema = "evidence-redaction-view.v1";

    public async ValueTask<Result<EvidenceRedactionView>> PrepareAsync(IRequestContext<PrepareEvidenceRedaction> context, CancellationToken ct)
    {
        try
        {
            return await PrepareCoreAsync(context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Unverified(error);
        }
    }

    public async ValueTask<Result<EvidenceRedactionView>> ApproveAsync(IRequestContext<ApproveEvidenceRedaction> context, CancellationToken ct)
    {
        try
        {
            return await ApproveCoreAsync(context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Unverified(error);
        }
    }

    static Result<EvidenceRedactionView> Unverified(ImmutableContentIntegrityException error) =>
        Result<EvidenceRedactionView>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The exact native redaction source or original response cannot be verified.", error is ImmutableContentUnavailableException));


    async ValueTask<Result<EvidenceRedactionView>> PrepareCoreAsync(IRequestContext<PrepareEvidenceRedaction> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!Valid(request.CommandId, request.TenantId, request.RedactionId, request.ExpectedRevision) ||
            request.OriginalArtifactId == Uuid.Empty || request.DerivedArtifactId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation, "A typed command, tenant, redaction, revision and artifact pair are required.");
        var allowed = await access.RequireAsync(context, request.TenantId, false, ct).ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return Result<EvidenceRedactionView>.Failure(allowed.Error!);
        allowed = await sources.RequireAccessAsync(context, request.TenantId, request.OriginalArtifactId, request.DerivedArtifactId, ct)
            .ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return Result<EvidenceRedactionView>.Failure(allowed.Error!);
        var actor = Actor(context, request.TenantId);
        var authored = request with { Provenance = request.Provenance?.Trim() ?? "", Reason = request.Reason?.Trim() ?? "" };
        var stream = new EvidenceRedaction(request.TenantId, request.RedactionId).Stream;
        var operation = Provenance(request.CommandId, nameof(PrepareEvidenceRedaction), stream, actor,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(authored), clock.GetUtcNow());
        PreparedMutation? prepared = null;
        var accepted = await operations.ExecuteAsync(new NativeOperation<EvidenceRedaction>(AcceptedOperationBinding.Of(operation),
            () => new EvidenceRedaction(request.TenantId, request.RedactionId, request.CommandId), owner => owner.Acceptance,
            async (owner, cancel) =>
            {
                if (owner.HasSelectedLegacy)
                    return Legacy();
                var cut = EvidenceRedactionPreparedCut.Of(owner);
                var history = await EvidenceRedactionHistory.ReadAsync(events, owner, content, resolver, cancel).ConfigureAwait(false);
                var captured = await sources.CaptureAsync(context, request.TenantId, request.OriginalArtifactId, request.DerivedArtifactId, cancel)
                    .ConfigureAwait(false);
                if (!captured.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(captured.Error!);
                if (!EvidenceRedactionRead.HasConsistentApprovalSources(history, captured.Value!))
                    return Refuse("The retained approval source differs from the observed derived artifact.");
                allowed = await access.RequireAsync(context, request.TenantId, false, cancel).ConfigureAwait(false);
                if (!allowed.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(allowed.Error!);
                var checkedSources = await sources.CheckAsync(context, captured.Value!, cancel).ConfigureAwait(false);
                if (!checkedSources.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(checkedSources.Error!);
                var at = clock.GetUtcNow();
                var decision = owner.DecidePrepare(request.CommandId, request.ExpectedRevision,
                    EvidenceRedactionSources.Identity(captured.Value!.Original), EvidenceRedactionSources.Identity(captured.Value.Derived),
                    authored.Provenance, authored.Reason, actor, at);
                if (!decision.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(decision.Error!);
                var proof = await proofs.FreezeAsync(owner, captured.Value, null, false, cancel).ConfigureAwait(false);
                checkedSources = await sources.CheckAsync(context, captured.Value, cancel).ConfigureAwait(false);
                if (!checkedSources.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(checkedSources.Error!);
                var view = history.With(decision.Value!).View(captured.Value.Original.Metadata.State,
                    captured.Value.Derived.Metadata.State, false);
                var frozen = await FreezeAsync(request.TenantId, view, cancel).ConfigureAwait(false);
                if (!frozen.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(frozen.Error!);
                prepared = new PreparedMutation(cut, operation with { AcceptedAt = at }, proof, frozen.Value.Result,
                    decision.Value, null);
                return Result<FirstResponseDescriptor>.Success(frozen.Value.Response);
            },
            (owner, first) =>
            {
                var mutation = prepared ?? throw new InvalidOperationException("Native redaction preparation has no prepared source cut.");
                return owner.Append(new EvidenceRedactionPreparedV2(request.TenantId, request.RedactionId, request.ExpectedRevision,
                    mutation.Preparation!, mutation.Operation, mutation.SourceProofs, first, mutation.Result), mutation.Cut);
            }), context, ct).ConfigureAwait(false);
        return await NativeContent.ResponseAsync<EvidenceRedactionView>(content, request.TenantId, accepted, ct).ConfigureAwait(false);
    }

    async ValueTask<Result<EvidenceRedactionView>> ApproveCoreAsync(IRequestContext<ApproveEvidenceRedaction> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Failure(RequestErrorKind.Forbidden, "Redaction approval requires a personal HTTP submission.");
        var request = context.Request;
        if (!Valid(request.CommandId, request.TenantId, request.RedactionId, request.ExpectedRevision) ||
            request.PreparationId == Uuid.Empty || request.PreparedRevision <= 0 || request.SeparationOfDutiesWaiverId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation, "A typed command and exact prepared redaction revision are required.");
        var allowed = await access.RequireAsync(context, request.TenantId, true, ct).ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return Result<EvidenceRedactionView>.Failure(allowed.Error!);
        var initial = await reader.HydrateAsync(new EvidenceRedaction(request.TenantId, request.RedactionId, request.CommandId), ct)
            .ConfigureAwait(false);
        if (initial.CurrentPreparation is not { } sourcePair)
            return Failure(RequestErrorKind.NotFound, "The redaction lineage was not found.");
        allowed = await sources.RequireAccessAsync(context, request.TenantId, sourcePair.Original.ArtifactId, sourcePair.Derived.ArtifactId, ct)
            .ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return Result<EvidenceRedactionView>.Failure(allowed.Error!);
        var actor = Actor(context, request.TenantId);
        var operation = Provenance(request.CommandId, nameof(ApproveEvidenceRedaction), initial.Stream, actor,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), clock.GetUtcNow());
        PreparedMutation? prepared = null;
        var accepted = await operations.ExecuteAsync(new NativeOperation<EvidenceRedaction>(AcceptedOperationBinding.Of(operation),
            () => new EvidenceRedaction(request.TenantId, request.RedactionId, request.CommandId), owner => owner.Acceptance,
            async (owner, cancel) =>
            {
                if (owner.HasSelectedLegacy)
                    return Legacy();
                var cut = EvidenceRedactionPreparedCut.Of(owner);
                var history = await EvidenceRedactionHistory.ReadAsync(events, owner, content, resolver, cancel).ConfigureAwait(false);
                if (owner.CurrentPreparation is not { } preparation || preparation.PreparationId != request.PreparationId ||
                    preparation.Revision != request.PreparedRevision)
                    return Refuse("Approve the exact current preparation revision.");
                var captured = await sources.CaptureAsync(context, request.TenantId, preparation.Original.ArtifactId, preparation.Derived.ArtifactId, cancel)
                    .ConfigureAwait(false);
                if (!captured.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(captured.Error!);
                var pair = captured.Value!;
                if (EvidenceRedactionSources.Identity(pair.Original) != preparation.Original ||
                    EvidenceRedactionSources.Identity(pair.Derived) != preparation.Derived ||
                    !EvidenceRedactionRead.HasConsistentApprovalSources(history, pair))
                    return Refuse("The observed sources differ from the exact retained lineage.");
                if (pair.Derived.Metadata.State != EvidenceArtifactStates.Available || pair.Derived.AvailabilityRecordedAt is not { } availableAt)
                    return Refuse("Approval requires the exact derived artifact to be currently available.");
                SeparationOfDutiesWaiver? waiver = null;
                EvidenceRedactionWaiverSnapshot? waiverValue = null;
                ulong waiverPosition = 0;
                if (request.SeparationOfDutiesWaiverId is { } waiverId)
                {
                    waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId, waiverId), cancel).ConfigureAwait(false);
                    waiverPosition = waiver.CommittedStreamPosition;
                    var eligible = await waivers.RequireAsync(waiver, owner, Uuid.Parse(actor.Id, System.Globalization.CultureInfo.InvariantCulture), clock.GetUtcNow(), cancel).ConfigureAwait(false);
                    if (!eligible.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(eligible.Error!);
                    waiverValue = Snapshot(waiver);
                    if (waiverValue is null)
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Forbidden, "The exact waiver is not independently approved."));
                }
                allowed = await access.RequireAsync(context, request.TenantId, true, cancel).ConfigureAwait(false);
                if (!allowed.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(allowed.Error!);
                var checkedSources = await sources.CheckAsync(context, pair, cancel).ConfigureAwait(false);
                if (!checkedSources.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(checkedSources.Error!);
                var at = clock.GetUtcNow();
                var decision = owner.DecideApprove(request.CommandId, request.ExpectedRevision, request.PreparationId,
                    request.PreparedRevision, pair.Derived.Metadata.SourcePosition, actor, at, availableAt, waiverValue);
                if (!decision.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(decision.Error!);
                var proof = await proofs.FreezeAsync(owner, pair, waiver, true, cancel).ConfigureAwait(false);
                checkedSources = await sources.CheckAsync(context, pair, cancel).ConfigureAwait(false);
                if (!checkedSources.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(checkedSources.Error!);
                if (waiver is not null)
                {
                    var current = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId, waiver.Id), cancel).ConfigureAwait(false);
                    if (current.CommittedStreamPosition != waiverPosition)
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict, "The waiver changed during this request.", true));
                    var eligible = await waivers.RequireAsync(current, owner, Uuid.Parse(actor.Id, System.Globalization.CultureInfo.InvariantCulture), clock.GetUtcNow(), cancel).ConfigureAwait(false);
                    if (!eligible.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(eligible.Error!);
                }
                var frozen = await FreezeAsync(request.TenantId, history.With(decision.Value!).View(pair.Original.Metadata.State,
                    pair.Derived.Metadata.State, true), cancel).ConfigureAwait(false);
                if (!frozen.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(frozen.Error!);
                prepared = new PreparedMutation(cut, operation with { AcceptedAt = at }, proof, frozen.Value.Result, null, decision.Value);
                return Result<FirstResponseDescriptor>.Success(frozen.Value.Response);
            },
            (owner, first) =>
            {
                var mutation = prepared ?? throw new InvalidOperationException("Native redaction approval has no prepared source cut.");
                var approval = mutation.Approval!;
                if (approval.SeparationOfDutiesWaiver is { } waiver &&
                    !EvidenceRedactionRules.ValidWaiver(request.TenantId, request.RedactionId, waiver, owner.CurrentPreparation!, actor, clock.GetUtcNow()))
                    return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "The exact waiver is no longer eligible at append."));
                return owner.Append(new EvidenceRedactionApprovedV2(request.TenantId, request.RedactionId, request.ExpectedRevision,
                    approval, mutation.Operation, mutation.SourceProofs, first, mutation.Result), mutation.Cut);
            }), context, ct).ConfigureAwait(false);
        return await NativeContent.ResponseAsync<EvidenceRedactionView>(content, request.TenantId, accepted, ct).ConfigureAwait(false);
    }

    async Task<Result<FrozenResult>> FreezeAsync(Uuid tenantId, EvidenceRedactionView value, CancellationToken ct)
    {
        // The existing store has a byte admission bound; it is not a history-count cap and no rows are truncated.
        if (JsonSerializer.SerializeToUtf8Bytes(value, ComplianceCoreJsonContext.Default.EvidenceRedactionView).LongLength >
            FitzKvImmutableContentStore.MaximumContentBytes)
            return Result<FrozenResult>.Failure(new RequestError(RequestErrorKind.Validation,
                "The complete original redaction response exceeds the existing immutable-content byte admission bound."));
        var frozen = await NativeContent.FreezeAsync(content, tenantId, ResultSchema, value, ct).ConfigureAwait(false);
        return Result<FrozenResult>.Success(new FrozenResult(frozen.Response, frozen.Result));
    }

    static EvidenceRedactionWaiverSnapshot? Snapshot(SeparationOfDutiesWaiver waiver) =>
        waiver.Scope is { } scope && waiver.ApproverMemberId is { } approver && waiver.ApprovedAt is { } approvedAt
            ? new(waiver.TenantId, waiver.Id, scope, waiver.BeneficiaryMemberId, waiver.RequesterMemberId, approver,
                waiver.RequestedAt, approvedAt, waiver.ExpiresAt)
            : null;
    static ActorReference Actor(IRequestContext context, Uuid tenantId)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            throw new InvalidOperationException("Authorized evidence decisions require the actual current canonical member.");
        return ActorReference.ForMember(RbacIds.Member(tenantId, user), UserIdentityClaims.BdgrzDisplay(context.Actor, user));
    }
    static bool Valid(Uuid command, Uuid tenant, Uuid redaction, long revision) =>
        command != Uuid.Empty && tenant != Uuid.Empty && redaction != Uuid.Empty && revision >= 0;
    static OperationProvenance Provenance(Uuid command, string kind, EventStreamAddress owner, ActorReference actor,
        long revision, string digest, DateTimeOffset at) => RootOperation.Provenance(command, kind, owner, actor, revision, digest, at) with { FirstOwner = owner };
    static Result<FirstResponseDescriptor> Legacy() => Refuse("The operation identity belongs to occupied unqualified v1 history; use a new typed command identity.");
    static Result<FirstResponseDescriptor> Refuse(string message) => Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict, message, false));
    static Result<EvidenceRedactionView> Failure(RequestErrorKind kind, string message) => Result<EvidenceRedactionView>.Failure(new RequestError(kind, message));
    sealed record FrozenResult(FirstResponseDescriptor Response, ImmutableContentRef Result);
    sealed record PreparedMutation(EvidenceRedactionPreparedCut Cut, OperationProvenance Operation, ImmutableContentRef SourceProofs,
        ImmutableContentRef Result, EvidenceRedactionPreparationFact? Preparation, EvidenceRedactionApprovalFact? Approval);
}
