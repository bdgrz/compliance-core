using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Reads and writes the immutable content referenced by access population, classification, expectation and
///     exception facts; the realm is the tenant. Every value is the complete original typed structure, so no list is
///     capped by an event size.
/// </summary>
static class AccessReviewContent
{
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
