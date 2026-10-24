using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Reads and writes the immutable content referenced by access review campaign and item facts; the realm is the
///     tenant. Every value is the complete original typed structure, so no list is capped by an event size.
/// </summary>
static class AccessReviewContent
{
    /// <summary>Schema of the reviewer list carried by a launch.</summary>
    public const string ReviewersSchema = "AccessReviewCampaignLaunched.ReviewersManifest.v1";

    /// <summary>Schema of the complete item list carried by a launch.</summary>
    public const string ItemsSchema = "AccessReviewCampaignLaunched.ItemsManifest.v1";

    /// <summary>Schema of the decision list carried by a decision fact.</summary>
    public const string DecisionsSchema = "AccessDecisionsRecorded.DecisionsManifest.v1";

    /// <summary>Schema of the complete provider change carried by a change fact.</summary>
    public const string ChangeSchema = "AccessRemediationChangeRecorded.ChangeContent.v1";

    /// <summary>Schema of the complete verification carried by a verification fact.</summary>
    public const string VerificationSchema = "AccessRemediationVerified.VerificationContent.v1";

    /// <summary>Schema of the complete exception carried by an exception fact.</summary>
    public const string ExceptionSchema = "AccessRemediationExceptionRecorded.ExceptionContent.v1";

    /// <summary>Schema of the complete reassignment carried by a reassignment fact.</summary>
    public const string ReassignmentSchema = "AccessReviewResponsibilityReassigned.ReassignmentContent.v1";

    /// <summary>Schema of the completion view carried by the closing fact.</summary>
    public const string CompletionSchema = "AccessReviewCampaignCompleted.CompletionContent.v1";

    /// <summary>Schema of the complete item proof set carried by the closing fact.</summary>
    public const string ItemProofsSchema = "AccessReviewCampaignCompleted.ItemCompletionProofs.v1";

    /// <summary>Schema of the authored completion decision frozen into the closure intent.</summary>
    public const string CompletionDecisionSchema = "access-review-campaign-completion-decision.v1";

    /// <summary>Schema of the failure proof that cancels a pending completion.</summary>
    public const string FailureProofSchema = "access-review-completion-failure-proof.v1";

    /// <summary>Schema of the facts a completion pins for one item.</summary>
    public const string ItemFactsSchema = "access-review-item-completion-facts.v1";

    /// <summary>Schema of the item view an item materializes from.</summary>
    public const string ItemEntrySchema = "AccessReviewItemCreationIntent.PopulationEntry.v1";

    /// <summary>Schema of the source use requirements a completion intent pins.</summary>
    public const string SourceUseItemSchema = "source-use-requirement.v1";

    /// <summary>Schema of the admitted children a completion intent drains (none until bulk batches exist).</summary>
    public const string AdmittedChildrenSchema = "access-review-decision-batch-admission.v1";

    /// <summary>Schema of the authored decision a decision fact keeps as its intent.</summary>
    public const string DecisionIntentSchema = "access-decision-intent.v1";

    /// <summary>Schema of the typed registration returned by a launch.</summary>
    public const string CampaignRegistrationSchema = "AccessReviewCampaignRegistration";

    /// <summary>Schema of the typed decision returned by a decision.</summary>
    public const string DecisionViewSchema = "AccessDecisionView";

    /// <summary>Schema of the typed change returned by a provider change.</summary>
    public const string ChangeViewSchema = "AccessRemediationChangeView";

    /// <summary>Schema of the typed verification returned by a verification.</summary>
    public const string VerificationViewSchema = "AccessRemediationVerificationView";

    /// <summary>Schema of the typed exception returned by an exception.</summary>
    public const string ExceptionViewSchema = "AccessRemediationExceptionView";

    /// <summary>Schema of the typed reassignment returned by a reassignment.</summary>
    public const string ReassignmentViewSchema = "AccessReviewResponsibilityReassignmentView";

    /// <summary>Schema of the typed completion returned by a completed campaign.</summary>
    public const string CompletionViewSchema = "AccessReviewCampaignCompletionView";

    /// <summary>Schema of the complete fact set carried by a population facts replacement.</summary>
    public const string FactsSchema = "AccessPopulationFactsRecorded.FactsContent.v1";

    /// <summary>Schema of the complete decision carried by a classification.</summary>
    public const string DecisionSchema = "AccessPrincipalClassified.DecisionContent.v1";

    /// <summary>Schema of the complete expectation carried by a proposal.</summary>
    public const string ExpectationSchema = "AccessExpectationProposed.ExpectationContent.v1";

    /// <summary>Schema of the approved expectation returned by an approval.</summary>
    public const string ApprovedExpectationSchema = "AccessExpectationApproved.ApprovedExpectation.v1";

    /// <summary>Schema of the complete expectation exception carried by an exception.</summary>
    public const string ExpectationExceptionSchema = "AccessExpectationExceptionRecorded.ExceptionContent.v1";

    /// <summary>Schema of the complete missing-population exception carried by an exception.</summary>
    public const string PopulationExceptionSchema = "AccessPopulationExceptionRecorded.ExceptionContent.v1";

    /// <summary>Schema of the registration returned by opening a population or replacing its facts.</summary>
    public const string RegistrationSchema = "AccessPopulationRegistration.v1";

    /// <summary>Schema of the acceptance returned by accepting a population.</summary>
    public const string AcceptanceSchema = "AccessPopulationAccepted.Acceptance.v1";

    public static string Realm(Uuid tenantId) => tenantId.ToString();

    public static Task<ImmutableContentRef> WriteAsync<T>(IImmutableContentStore content, Uuid tenantId, string schema,
        T value, CancellationToken ct) where T : notnull =>
        content.WriteJsonAsync(Realm(tenantId), Uuid.CreateVersion4(), schema, value, ct);

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

    /// <summary>Writes a typed value as the body of a frozen 200 response and as the original operation result.</summary>
    public static async Task<(FirstResponseDescriptor Response, ImmutableContentRef Result)> FreezeOkAsync<T>(
        IImmutableContentStore content, Uuid tenantId, string schema, T value, CancellationToken ct) where T : notnull
    {
        var body = await WriteAsync(content, tenantId, schema, value, ct).ConfigureAwait(false);
        var result = await WriteResultAsync(content, tenantId, schema, body, ct).ConfigureAwait(false);
        return (OkResponse(schema, body), result);
    }

    /// <summary>Where a caller reads the status of an accepted operation.</summary>
    public const string StatusUrlTemplate = "/api/v1/platform/operations/{operation_id}";

    /// <summary>The frozen 202 first response of a root admission.</summary>
    public static async Task<FirstResponseDescriptor> AcceptedResponseAsync(IImmutableContentStore content,
        Uuid tenantId, Uuid operationId, DateTimeOffset acceptedAt, CancellationToken ct)
    {
        var acknowledgment = await content.WriteJsonAsync(Realm(tenantId), Uuid.CreateVersion4(),
            "operation-accepted-response.v1", new OperationAcceptedResponse(operationId,
                StatusUrlTemplate.Replace("{operation_id}", operationId.ToString(), StringComparison.Ordinal),
                acceptedAt), ct).ConfigureAwait(false);
        return new FirstResponseDescriptor(202, ImmutableContentStoreExtensions.JsonMediaType,
            "operation-accepted-response.v1", new StandardResponseBody(acknowledgment));
    }

    /// <summary>The 202 acknowledgment of an accepted or replayed admission, read from the frozen first response.</summary>
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
            throw new InvalidOperationException("An access review acknowledgment is standard content.");
        return Result<OperationAcceptedResponse>.Success(await content
            .ReadJsonAsync<OperationAcceptedResponse>(Realm(tenantId), body.Content, ct).ConfigureAwait(false));
    }

    /// <summary>The typed value of an accepted or replayed 200 admission, read from its frozen first response.</summary>
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
            throw new InvalidOperationException("An access review response is standard content.");
        return Result<T>.Success(await ReadAsync<T>(content, tenantId, body.Content, ct).ConfigureAwait(false));
    }
}
