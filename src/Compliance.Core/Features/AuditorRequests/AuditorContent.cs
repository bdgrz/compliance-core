using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Reads and writes the immutable content referenced by auditor request facts; the realm is the tenant. Every value
///     is the complete original typed structure, so no list is capped by an event size.
/// </summary>
static class AuditorContent
{
    public const string TermsSchema = "AuditorRequestRecorded.TermsContent.v1";
    public const string ReopenSchema = "AuditorRequestReopened.ReopenContent.v1";
    public const string ResponseDraftSchema = "AuditorResponseAdmitted.DraftContent.v1";
    public const string CommentSchema = "AuditorCommentAdmitted.CommentContent.v1";
    public const string VersionSchema = "AuditResponseDraftRevised.VersionContent.v1";
    public const string SubmissionSchema = "AuditorResponseSubmitted.SubmissionContent.v1";
    public const string ReviewSchema = "AuditorResponseDisclosureReviewed.ReviewContent.v1";
    public const string DeliverySchema = "AuditorResponseDelivered.DeliveryContent.v1";
    public const string DispositionSchema = "AuditorResponseDispositionRecorded.DispositionContent.v1";
    public const string DecisionSchema = "ResolveAuditorRequest.AuthoredDecision.v1";
    public const string AdmittedChildSchema = "admitted-request-child.v1";
    public const string ResponseFactsSchema = "AuditorResponse.ResolutionFacts.v1";
    public const string ProofsSchema = "AuditorRequestResolved.ResponseDispositionProofs.v1";
    public const string FailureSchema = "AuditorRequestResolutionCanceled.FailureProof.v1";
    public const string PopulationVersionSchema = "AuditPopulationVersionView.v1";
    public const string PopulationRowSchema = "population-row-id.v1";
    public const string AcknowledgmentSchema = "operation-accepted-response.v1";

    public static string Realm(Uuid tenantId) => tenantId.ToString();

    public static Task<ImmutableContentRef> WriteAsync<T>(IImmutableContentStore content, Uuid tenantId, string schema,
        T value, CancellationToken ct) where T : notnull =>
        content.WriteJsonAsync(Realm(tenantId), Uuid.CreateVersion4(), schema, value, ct);

    /// <summary>Writes content under a content id derived from <paramref name="identity" />, so a replay writes the same bytes once.</summary>
    public static Task<ImmutableContentRef> WriteDeterministicAsync<T>(IImmutableContentStore content, Uuid tenantId,
        Uuid identity, string schema, T value, CancellationToken ct) where T : notnull =>
        content.WriteJsonAsync(Realm(tenantId), Uuid.CreateVersion5(identity, schema), schema, value, ct);

    public static Task<T> ReadAsync<T>(IImmutableContentStore content, Uuid tenantId, ImmutableContentRef reference,
        CancellationToken ct) where T : notnull => content.ReadJsonAsync<T>(Realm(tenantId), reference, ct);

    /// <summary>The frozen 200 first response and operation result of an immediate Unit command.</summary>
    public static async Task<(FirstResponseDescriptor Response, ImmutableContentRef Result)> UnitAsync(
        IImmutableContentStore content, Uuid tenantId, CancellationToken ct)
    {
        var body = await WriteAsync(content, tenantId, UnitResult.Schema, new UnitResult(), ct).ConfigureAwait(false);
        var result = await WriteAsync(content, tenantId, "mutation-result-content.v1",
            new MutationResultContent(UnitResult.Schema, new StandardResponseBody(body)), ct).ConfigureAwait(false);
        return (new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType, UnitResult.Schema,
            new StandardResponseBody(body)), result);
    }

    /// <summary>The Unit result a terminal fact of an operation carries, written once per root operation.</summary>
    public static async Task<ImmutableContentRef> UnitResultAsync(IImmutableContentStore content, Uuid tenantId,
        Uuid rootOperationId, CancellationToken ct)
    {
        var unit = await WriteDeterministicAsync(content, tenantId, rootOperationId, UnitResult.Schema,
            new UnitResult(), ct).ConfigureAwait(false);
        return await WriteDeterministicAsync(content, tenantId, rootOperationId, "mutation-result-content.v1",
            new MutationResultContent(UnitResult.Schema, new StandardResponseBody(unit)), ct).ConfigureAwait(false);
    }

    /// <summary>Where a caller reads the status of an operation accepted in this tenant.</summary>
    public static string StatusUrl(Uuid tenantId, Uuid operationId) =>
        $"/api/v1/tenants/{tenantId}/operations/{operationId}";

    /// <summary>The empty selected source proof manifest of an admission that selected no external source.</summary>
    public static Task<ImmutableSelectionManifest> EmptyProofsAsync(IImmutableContentStore content, Uuid tenantId,
        Uuid identity, CancellationToken ct) =>
        ImmutableSelectionManifests.WriteAsync<NativeFactReference>(content, tenantId.ToString(),
            Uuid.CreateVersion5(identity, "selected-source-proofs"), "source-fact-reference.v1", [],
            SelectionOrder.CanonicalKey, ct);

    /// <summary>The frozen 202 acknowledgment of an admission.</summary>
    public static async Task<FirstResponseDescriptor> AcceptedResponseAsync(IImmutableContentStore content,
        Uuid tenantId, Uuid operationId, DateTimeOffset acceptedAt, CancellationToken ct)
    {
        var body = await WriteAsync(content, tenantId, AcknowledgmentSchema,
            new OperationAcceptedResponse(operationId, StatusUrl(tenantId, operationId), acceptedAt),
            ct).ConfigureAwait(false);
        return new FirstResponseDescriptor(202, ImmutableContentStoreExtensions.JsonMediaType, AcknowledgmentSchema,
            new StandardResponseBody(body));
    }

    /// <summary>The typed value of an accepted or replayed admission, read from its frozen first response.</summary>
    public static async Task<Result<T>> ResponseAsync<T>(IImmutableContentStore content, Uuid tenantId,
        Result<AcceptedAppendResult> outcome, CancellationToken ct) where T : notnull
    {
        if (!outcome.IsSuccess)
            return Result<T>.Failure(outcome.Error!);
        var descriptor = outcome.Value switch
        {
            AcceptedAppendResult.Committed committed => committed.Response,
            AcceptedAppendResult.Replayed replayed => replayed.Response,
            _ => null,
        };
        if (descriptor is null)
            return Result<T>.Failure(AcceptedAppendResult.ConflictError);
        if (descriptor.Body is not StandardResponseBody body)
            throw new InvalidOperationException("An auditor request response is standard content.");
        return Result<T>.Success(await ReadAsync<T>(content, tenantId, body.Content, ct).ConfigureAwait(false));
    }

    /// <summary>Success for an accepted or replayed Unit command; a conflicting reuse of the identity otherwise.</summary>
    public static Result UnitOutcome(Result<AcceptedAppendResult> outcome) => !outcome.IsSuccess
        ? Result.Failure(outcome.Error!)
        : outcome.Value is AcceptedAppendResult.Committed or AcceptedAppendResult.Replayed
            ? Result.Success
            : Result.Failure(AcceptedAppendResult.ConflictError);

    /// <summary>The provenance of a root operation on a request, response, thread or selection owner.</summary>
    public static OperationProvenance Provenance(Uuid logicalId, string commandKind, EventStreamAddress owner,
        AccessControl.ActorReference actor, ulong? expectedRevision, string intentDigest, DateTimeOffset acceptedAt) =>
        new(logicalId, logicalId, null, commandKind, actor, owner, "initial", expectedRevision, intentDigest,
            acceptedAt, owner, owner);

    public static DurableIntentHeader Header(OperationProvenance root, DateTimeOffset acceptedAt) => new(
        root.RootOperationId, root.Owner, root.AffectedOwner, root.ExpectedRevisionOwner, root.Actor, root.Episode,
        root.OriginalExpectedRevision, root.AuthoredIntentSha256, acceptedAt);

    /// <summary>The first response of a prepared command, or a retryable conflict when the owner changed while it was prepared.</summary>
    public static Result<FirstResponseDescriptor> Prepared(Result guard, FirstResponseDescriptor? response) =>
        !guard.IsSuccess
            ? Result<FirstResponseDescriptor>.Failure(guard.Error!)
            : response is null
                ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The record changed while the command was prepared; reload and retry.", true))
                : Result<FirstResponseDescriptor>.Success(response);
}
