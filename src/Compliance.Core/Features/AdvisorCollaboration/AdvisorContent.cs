using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>
///     Reads and writes the immutable content referenced by handoff and feedback thread facts and shapes their
///     operation provenance; the realm is the tenant. Every value is the complete original typed structure, so no
///     selection is capped by an event size.
/// </summary>
static class AdvisorContent
{
    public const string CreatedSelectionSchema = "AdvisorHandoffCreated.RecordVersionsManifest.v1";
    public const string RevisedSelectionSchema = "AdvisorHandoffSelectionRevised.RecordVersionsManifest.v1";
    public const string FeedbackAuthoredSchema = "RecordAdvisorFeedback.AuthoredContent.v1";
    public const string AcknowledgmentSchema = "operation-accepted-response.v1";
    public const string InitialEpisode = "initial";

    public static string Realm(Uuid tenantId) => tenantId.ToString();

    /// <summary>Prepares content under a fresh identity; only the owner's append decides whether it is accepted.</summary>
    public static Task<ImmutableContentRef> WriteAsync<T>(IImmutableContentStore content, Uuid tenantId,
        string schema, T value, CancellationToken ct) where T : notnull =>
        content.WriteJsonAsync(Realm(tenantId), Uuid.CreateVersion4(), schema, value, ct);

    public static Task<T> ReadAsync<T>(IImmutableContentStore content, Uuid tenantId, ImmutableContentRef reference,
        CancellationToken ct) where T : notnull => content.ReadJsonAsync<T>(Realm(tenantId), reference, ct);

    /// <summary>Writes a selection under a fresh content id: two selections of one handoff differ by generation.</summary>
    public static Task<ImmutableContentRef> WriteSelectionAsync(IImmutableContentStore content, Uuid tenantId,
        string schema, IReadOnlyList<AdvisorRecordVersion> selection, CancellationToken ct) =>
        content.WriteJsonAsync(Realm(tenantId), Uuid.CreateVersion4(), schema, selection, ct);

    /// <summary>The provenance of a root operation on a handoff or thread owner.</summary>
    public static OperationProvenance Provenance(Uuid logicalId, string commandKind, EventStreamAddress owner,
        ActorReference actor, ulong? expectedRevision, string intentDigest, DateTimeOffset acceptedAt) =>
        new(logicalId, logicalId, null, commandKind, actor, owner, InitialEpisode, expectedRevision, intentDigest,
            acceptedAt, owner, owner);

    public static DurableIntentHeader Header(OperationProvenance root, DateTimeOffset acceptedAt) => new(
        root.RootOperationId, root.Owner, root.AffectedOwner, root.ExpectedRevisionOwner, root.Actor, root.Episode,
        root.OriginalExpectedRevision, root.AuthoredIntentSha256, acceptedAt);

    /// <summary>The frozen 200 first response and the mutation result of an operation whose declared result is Unit.</summary>
    public static async Task<(FirstResponseDescriptor Response, ImmutableContentRef Result)> FreezeUnitAsync(
        IImmutableContentStore content, Uuid tenantId, CancellationToken ct)
    {
        var body = await content.WriteJsonAsync(Realm(tenantId), Uuid.CreateVersion4(), UnitResult.Schema,
            new UnitResult(), ct).ConfigureAwait(false);
        var result = await content.WriteJsonAsync(Realm(tenantId), Uuid.CreateVersion4(), "mutation-result-content.v1",
            new MutationResultContent(UnitResult.Schema, new StandardResponseBody(body)), ct).ConfigureAwait(false);
        var response = new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType,
            UnitResult.Schema, new StandardResponseBody(body));
        return (response, result);
    }

    /// <summary>The frozen 202 acknowledgment of an admission.</summary>
    public static async Task<FirstResponseDescriptor> AcceptedResponseAsync(IImmutableContentStore content, Uuid tenantId,
        Uuid operationId, string statusUrlTemplate, DateTimeOffset acceptedAt, CancellationToken ct)
    {
        var body = await content.WriteJsonAsync(Realm(tenantId), Uuid.CreateVersion4(), AcknowledgmentSchema,
            new OperationAcceptedResponse(operationId,
                statusUrlTemplate.Replace("{operation_id}", operationId.ToString(), StringComparison.Ordinal),
                acceptedAt), ct).ConfigureAwait(false);
        return new FirstResponseDescriptor(202, ImmutableContentStoreExtensions.JsonMediaType, AcknowledgmentSchema,
            new StandardResponseBody(body));
    }

    /// <summary>The acknowledgment of an accepted or replayed admission, read from its frozen first response.</summary>
    public static async Task<Result<OperationAcceptedResponse>> AcknowledgmentAsync(IImmutableContentStore content,
        Uuid tenantId, Result<AcceptedAppendResult> outcome, CancellationToken ct)
    {
        if (!outcome.IsSuccess)
            return Result<OperationAcceptedResponse>.Failure(outcome.Error!);
        var descriptor = outcome.Value switch
        {
            AcceptedAppendResult.Committed committed => committed.Response,
            AcceptedAppendResult.Replayed replayed => replayed.Response,
            _ => null,
        };
        if (descriptor is null)
            return Result<OperationAcceptedResponse>.Failure(AcceptedAppendResult.ConflictError);
        if (descriptor.Body is not StandardResponseBody body)
            throw new InvalidOperationException("An advisor response is standard content.");
        return Result<OperationAcceptedResponse>.Success(await ReadAsync<OperationAcceptedResponse>(content, tenantId,
            body.Content, ct).ConfigureAwait(false));
    }

    /// <summary>The empty selected source proof manifest of an admission that selected no external source.</summary>
    public static Task<ImmutableSelectionManifest> EmptyProofsAsync(IImmutableContentStore content, Uuid tenantId,
        Uuid identity, CancellationToken ct) =>
        ImmutableSelectionManifests.WriteAsync<NativeFactReference>(content, Realm(tenantId),
            Uuid.CreateVersion5(identity, "selected-source-proofs"), "source-fact-reference.v1", [],
            SelectionOrder.CanonicalKey, ct);

    /// <summary>The unit outcome of an accepted or replayed operation, or the conflict of a changed intent.</summary>
    public static Result UnitOutcome(Result<AcceptedAppendResult> outcome)
    {
        if (!outcome.IsSuccess)
            return Result.Failure(outcome.Error!);
        return outcome.Value switch
        {
            AcceptedAppendResult.Committed or AcceptedAppendResult.Replayed => Result.Success,
            _ => Result.Failure(AcceptedAppendResult.ConflictError),
        };
    }

    /// <summary>The first response of a prepared command, or a retryable conflict when the owner changed while it was prepared.</summary>
    public static Result<FirstResponseDescriptor> Prepared(Result guard, FirstResponseDescriptor? response) =>
        !guard.IsSuccess
            ? Result<FirstResponseDescriptor>.Failure(guard.Error!)
            : response is null
                ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The record changed while the command was prepared; reload and retry.", true))
                : Result<FirstResponseDescriptor>.Success(response);
}
