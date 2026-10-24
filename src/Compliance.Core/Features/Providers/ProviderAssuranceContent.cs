using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     Immutable content and frozen responses of the provider assurance owners. Content lives in the realm of its
///     tenant; every event carries a verified reference, never the structured value.
/// </summary>
static class ProviderAssuranceContent
{
    public const string ReportRecordedSchema = "AssuranceReportRecorded.ContentContent.v1";
    public const string ReportRevisedSchema = "AssuranceReportRevised.ContentContent.v1";
    public const string ReviewRecordedSchema = "ProviderReviewRecorded.ContentContent.v1";
    public const string GapRecordedSchema = "ProviderCoverageGapRecorded.ContentContent.v1";
    public const string GapClosedSchema = "ProviderCoverageGapClosed.ContentContent.v1";
    public const string LinkSchema = "ProviderCoverageGapRiskAcceptanceLinked.AcceptanceContent.v1";
    public const string FailureProofSchema = "provider-review-failure-proof.v1";
    public const string ReportRegistrationSchema = "assurance-report-registration.v1";
    public const string GapRegistrationSchema = "provider-coverage-gap-registration.v1";
    public const string ReviewRegistrationSchema = "provider-review-registration.v1";
    public const string AcceptedSchema = "operation-accepted-response.v1";
    public const string StatusUrlTemplate = "/api/v1/tenants/{tenant_id}/operations/{operation_id}";

    public static string Realm(Uuid tenantId) => tenantId.ToString();

    public static Task<ImmutableContentRef> WriteAsync<T>(IImmutableContentStore content, Uuid tenantId, string schema,
        T value, CancellationToken ct, Uuid? contentId = null) where T : notnull =>
        content.WriteJsonAsync(Realm(tenantId), contentId ?? Uuid.CreateVersion4(), schema, value, ct);

    public static Task<T> ReadAsync<T>(IImmutableContentStore content, Uuid tenantId, ImmutableContentRef reference,
        CancellationToken ct) where T : notnull => content.ReadJsonAsync<T>(Realm(tenantId), reference, ct);

    /// <summary>Freezes a completed result as the HTTP 200 first response of an immediate command.</summary>
    public static FirstResponseDescriptor Completed(ImmutableContentRef body, string schema) =>
        new(200, ImmutableContentStoreExtensions.JsonMediaType, schema, new StandardResponseBody(body));

    /// <summary>Writes the fixed 202 acknowledgment and describes it as the first response of an admission.</summary>
    public static async Task<FirstResponseDescriptor> AcceptedAsync(IImmutableContentStore content, Uuid tenantId,
        Uuid operationId, DateTimeOffset acceptedAt, CancellationToken ct)
    {
        var acknowledgment = new OperationAcceptedResponse(operationId, StatusUrlTemplate
            .Replace("{tenant_id}", tenantId.ToString(), StringComparison.Ordinal)
            .Replace("{operation_id}", operationId.ToString(), StringComparison.Ordinal), acceptedAt);
        var body = await WriteAsync(content, tenantId, AcceptedSchema, acknowledgment, ct).ConfigureAwait(false);
        return new FirstResponseDescriptor(202, ImmutableContentStoreExtensions.JsonMediaType, AcceptedSchema,
            new StandardResponseBody(body));
    }

    /// <summary>The response a committed or replayed acceptance froze, read back as verified content.</summary>
    public static async Task<Result<T>> ReadFirstResponseAsync<T>(IImmutableContentStore content, Uuid tenantId,
        string schema, Result<AcceptedAppendResult> accepted, CancellationToken ct) where T : notnull
    {
        if (!accepted.IsSuccess)
            return Result<T>.Failure(accepted.Error!);
        var descriptor = accepted.Value switch
        {
            AcceptedAppendResult.Committed committed => committed.Response,
            AcceptedAppendResult.Replayed replayed => replayed.Response,
            _ => null,
        };
        if (descriptor is null)
            return Result<T>.Failure(AcceptedAppendResult.ConflictError);
        if (descriptor.Body is not StandardResponseBody body || descriptor.ResultSchema != schema)
            throw new ImmutableContentIntegrityException("The accepted provider assurance operation froze an unexpected response.");
        return Result<T>.Success(await ReadAsync<T>(content, tenantId, body.Content, ct).ConfigureAwait(false));
    }
}
