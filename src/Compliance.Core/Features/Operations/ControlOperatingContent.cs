using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Reads and writes the immutable content referenced by operating plan and occurrence facts; the realm is the
///     tenant. Every value is the complete original typed structure, so no list is capped by an event size.
/// </summary>
static class ControlOperatingContent
{
    /// <summary>Schema of the complete plan carried by a proposal.</summary>
    public const string PlanSchema = "ControlOperatingPlanProposed.PlanContent.v1";

    /// <summary>Schema of the approved plan view returned by an approval.</summary>
    public const string ApprovedPlanSchema = "ControlOperatingPlanApproved.ApprovedPlan.v1";

    /// <summary>Schema of the reassignment list carried by an approval.</summary>
    public const string ReassignmentsSchema = "ControlOperatingPlanApproved.ReassignmentsManifest.v1";

    /// <summary>Schema of the complete attestation version carried by an attestation.</summary>
    public const string AttestationSchema = "ControlOccurrenceAttested.AttestationContent.v1";

    /// <summary>Schema of the complete review decision carried by a review.</summary>
    public const string ReviewSchema = "ControlOccurrenceReviewed.ReviewContent.v1";

    /// <summary>Schema of the occurrence view returned by an occurrence operation.</summary>
    public const string OccurrenceViewSchema = "ControlOccurrence.OccurrenceView.v1";

    /// <summary>Schema of the canonical occurrence id list of a generation.</summary>
    public const string OccurrenceIdsSchema = "DueControlOccurrencesGenerated.OccurrenceIdsManifest.v1";

    public static string Realm(Uuid tenantId) => tenantId.ToString();

    public static Task<ImmutableContentRef> WriteAsync<T>(IImmutableContentStore content, Uuid tenantId,
        string schema, T value, CancellationToken ct) where T : notnull =>
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

    /// <summary>The frozen 200 first response of an operation that returns no value.</summary>
    public static async Task<FirstResponseDescriptor> UnitResponseAsync(IImmutableContentStore content,
        Uuid tenantId, CancellationToken ct)
    {
        var unit = await WriteAsync(content, tenantId, UnitResult.Schema, new UnitResult(), ct)
            .ConfigureAwait(false);
        return OkResponse(UnitResult.Schema, unit);
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
            throw new InvalidOperationException("An operating response is standard content.");
        return Result<T>.Success(await ReadAsync<T>(content, tenantId, body.Content, ct).ConfigureAwait(false));
    }
}
