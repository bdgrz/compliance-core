using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     Reads and writes the immutable content referenced by readiness facts; the realm is the tenant. Every value is the
///     complete original typed structure, so no list is capped by an event size.
/// </summary>
static class ReadinessContent
{
    public const string InputsSchema = "ReadinessAssessmentRecorded.InputsManifest.v1";
    public const string FindingsSchema = "ReadinessAssessmentRecorded.FindingsManifest.v1";
    public const string GapsSchema = "ReadinessAssessmentRecorded.GapsManifest.v1";
    public const string AccessReviewScopesSchema = "ReadinessAssessmentRecorded.AccessReviewScopesManifest.v1";
    public const string PlanSchema = "ReadinessGapPlanned.PlanContent.v1";
    public const string AnnotationSchema = "ReadinessGapAnnotated.AnnotationContent.v1";
    public const string ReadinessDecisionSchema = "ReadinessDecisionRecorded.DecisionContent.v1";
    public const string TypeIEntryDecisionSchema = "TypeIEntryDecisionRecorded.DecisionContent.v1";
    public const string DecisionProofsSchema = "ReadinessDecisionRecorded.DecisionProofs.v1";
    public const string AdoptionProofsSchema = "ReadinessAssessmentAdopted.SourceProofs.v1";
    public const string FailureSchema = "ReadinessDecisionCanceled.FailureProof.v2";
    public const string WaiverProofSchema = "separation-of-duties-waiver-proof.v1";
    public const string PlanFactsSchema = "readiness-plan-facts.v1";
    public const string GapItemSchema = "readiness-gap-view.v1";
    public const string AcknowledgedGapSchema = "readiness-acknowledged-gap.v1";
    public const string RegistrationSchema = "Readiness.AssessmentRegistration.v1";
    public const string PlanViewSchema = "Readiness.GapPlanView.v1";
    public const string AnnotationViewSchema = "Readiness.AnnotationView.v1";
    public const string ReadinessDecisionViewSchema = "Readiness.DecisionView.v1";
    public const string TypeIEntryDecisionViewSchema = "Readiness.TypeIEntryDecisionView.v1";
    public const string AcknowledgmentSchema = "operation-accepted-response.v1";

    /// <summary>Classifies expected immutable content failures after native acceptance recovery has had its recheck.</summary>
    public static async ValueTask<Result<T>> ClassifyAsync<T>(Func<ValueTask<Result<T>>> execute)
    {
        try
        {
            return await execute().ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<T>.Failure(GetReadinessAssessmentHandler.Unverified(exception));
        }
    }

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

    /// <summary>Writes the mutation result that wraps an already written response body.</summary>
    public static Task<ImmutableContentRef> WriteResultAsync(IImmutableContentStore content, Uuid tenantId,
        string schema, ImmutableContentRef body, CancellationToken ct) =>
        content.WriteJsonAsync(Realm(tenantId), Uuid.CreateVersion4(), "mutation-result-content.v1",
            new MutationResultContent(schema, new StandardResponseBody(body)), ct);

    /// <summary>The frozen 200 first response whose body is the given content.</summary>
    public static FirstResponseDescriptor OkResponse(string schema, ImmutableContentRef body) => new(200,
        ImmutableContentStoreExtensions.JsonMediaType, schema, new StandardResponseBody(body));

    /// <summary>Freezes a typed value as the 200 first response and the matching mutation result.</summary>
    public static async Task<(FirstResponseDescriptor Response, ImmutableContentRef Result)> FreezeAsync<T>(
        IImmutableContentStore content, Uuid tenantId, string schema, T value, CancellationToken ct) where T : notnull
    {
        var body = await WriteAsync(content, tenantId, schema, value, ct).ConfigureAwait(false);
        var result = await WriteResultAsync(content, tenantId, schema, body, ct).ConfigureAwait(false);
        return (OkResponse(schema, body), result);
    }

    /// <summary>The frozen 202 acknowledgment of an admission.</summary>
    public static async Task<FirstResponseDescriptor> AcceptedResponseAsync(IImmutableContentStore content, Uuid tenantId,
        Uuid operationId, string statusUrlTemplate, DateTimeOffset acceptedAt, CancellationToken ct)
    {
        var body = await WriteAsync(content, tenantId, AcknowledgmentSchema, new OperationAcceptedResponse(operationId,
            statusUrlTemplate.Replace("{operation_id}", operationId.ToString(), StringComparison.Ordinal), acceptedAt),
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
            throw new InvalidOperationException("A readiness response is standard content.");
        return Result<T>.Success(await ReadAsync<T>(content, tenantId, body.Content, ct).ConfigureAwait(false));
    }

    /// <summary>The provenance of a root operation on a readiness owner.</summary>
    public static OperationProvenance Provenance(Uuid logicalId, string commandKind, EventStreamAddress owner,
        AccessControl.ActorReference actor, ulong? expectedRevision, string intentDigest, DateTimeOffset acceptedAt) =>
        new(logicalId, logicalId, null, commandKind, actor, owner, "initial", expectedRevision, intentDigest,
            acceptedAt, owner, owner);

    public static DurableIntentHeader Header(OperationProvenance root, DateTimeOffset acceptedAt) => new(
        root.RootOperationId, root.Owner, root.AffectedOwner, root.ExpectedRevisionOwner, root.Actor, root.Episode,
        root.OriginalExpectedRevision, root.AuthoredIntentSha256, acceptedAt);

    /// <summary>The member that acted, as frozen in the header of a root operation.</summary>
    public static Uuid MemberOf(DurableIntentHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        return header.OriginalActor is { Kind: "member" } actor &&
               Uuid.TryParse(actor.Id, System.Globalization.CultureInfo.InvariantCulture, out var member)
            ? member
            : throw new ImmutableContentIntegrityException("The frozen actor is not a tenant member.");
    }
}
