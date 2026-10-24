using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     Provenance and frozen first responses shared by the local inventory operations of declared applications and
///     declared system instances. Every operation is the root acceptance of its logical id on its own owner stream.
/// </summary>
static class ApplicationInventoryOperations
{
    /// <summary>The episode of every inventory operation: inventory owners have no membership episodes.</summary>
    public const string InitialEpisode = "initial";

    /// <summary>Schema of the frozen application registration.</summary>
    public const string ApplicationRegistrationSchema = "ApplicationRegistration.v1";

    /// <summary>Schema of the frozen system instance registration.</summary>
    public const string SystemInstanceRegistrationSchema = "SystemInstanceRegistration.v1";

    /// <summary>
    ///     The provenance of a root inventory operation on its own owner; a retry's binding derives from it. The
    ///     original actor is the member identity alone: a retry after the member's display name changed is still the
    ///     same actor, and the fact keeps the display it was recorded with in its own attribution fields.
    /// </summary>
    public static OperationProvenance Provenance(Uuid logicalId, string commandKind, EventStreamAddress owner,
        Uuid memberId, long? expectedRevision, string intentDigest, DateTimeOffset acceptedAt) => new(
        logicalId, logicalId, null, commandKind, ActorReference.ForMember(memberId, string.Empty), owner,
        InitialEpisode, expectedRevision is { } expected ? (ulong)expected : null, intentDigest, acceptedAt, owner,
        owner);

    /// <summary>Trims text and treats blank optional text as absent, exactly as the owners normalize it.</summary>
    public static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>The frozen 200 first response whose body is a typed registration.</summary>
    public static async Task<FirstResponseDescriptor> RegistrationAsync<T>(IImmutableContentStore content,
        Uuid tenantId, string schema, T registration, CancellationToken ct) where T : notnull
    {
        var body = await ApplicationImportContent.WriteAsync(content, tenantId, schema, registration, ct)
            .ConfigureAwait(false);
        return ApplicationImportContent.OkResponse(schema, body);
    }

    /// <summary>The frozen 200 first response of an operation that returns no value.</summary>
    public static async Task<FirstResponseDescriptor> UnitAsync(IImmutableContentStore content, Uuid tenantId,
        CancellationToken ct) => (await ApplicationImportContent.UnitAsync(content, tenantId, ct)
        .ConfigureAwait(false)).Response;

    /// <summary>The prepared half of an acceptance: the frozen response once guards pass, else a transient retry.</summary>
    public static Result<FirstResponseDescriptor> Prepared(Result guards, FirstResponseDescriptor? response) =>
        !guards.IsSuccess
            ? Result<FirstResponseDescriptor>.Failure(guards.Error!)
            : response is null
                ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The record changed while the operation was prepared; retry.", true))
                : Result<FirstResponseDescriptor>.Success(response);

    /// <summary>The closed result of a guard that reports a command failure.</summary>
    public static Result Of(CommandFailure? failure) =>
        failure is null ? Result.Success : Result.Failure(failure.ToRequestError());

    /// <summary>The closed Unit outcome of an accepted or replayed immediate operation.</summary>
    public static Result UnitOutcome(Result<AcceptedAppendResult> outcome) =>
        ApplicationImportContent.UnitOutcome(outcome);
}
