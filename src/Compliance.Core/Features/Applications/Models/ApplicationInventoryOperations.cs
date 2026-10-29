using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Applications.Models;

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
