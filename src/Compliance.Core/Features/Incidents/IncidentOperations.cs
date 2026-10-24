using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Shared pieces of the identified incident operations: provenance, frozen Unit response and outcome.</summary>
static class IncidentOperations
{
    /// <summary>The single lifecycle episode of an incident.</summary>
    const string IncidentEpisode = "incident";

    /// <summary>Validates the identities and signed expected revision every incident command carries.</summary>
    public static RequestError? Validate(Uuid commandId, Uuid tenantId, Uuid programId, Uuid incidentId,
        long? expectedRevision)
    {
        if (commandId == Uuid.Empty || tenantId == Uuid.Empty || programId == Uuid.Empty || incidentId == Uuid.Empty)
            return new RequestError(RequestErrorKind.Validation,
                "A command id, tenant, program and incident are required.");
        return expectedRevision < 0
            ? new RequestError(RequestErrorKind.Validation, "The expected revision must not be negative.")
            : null;
    }

    /// <summary>The provenance one incident command carries; the binding of a retry is derived from it.</summary>
    public static OperationProvenance Provenance<TRequest>(TRequest request, string commandKind, Uuid commandId,
        Uuid tenantId, Uuid programId, Uuid incidentId, long? expectedRevision, ActorReference actor,
        DateTimeOffset acceptedAt) where TRequest : notnull
    {
        var owner = Owner(tenantId, programId, incidentId, Uuid.Empty).Stream;
        return new OperationProvenance(commandId, commandId, null, commandKind, actor, owner, IncidentEpisode,
            expectedRevision is { } revision ? (ulong)revision : null, AuthoredIntentDigest.Compute(request),
            acceptedAt, owner, owner);
    }

    /// <summary>The incident hydrated for one selected logical operation.</summary>
    public static ComplianceIncident Owner(Uuid tenantId, Uuid programId, Uuid incidentId, Uuid selectedCommandId) =>
        new(tenantId, programId, incidentId, selectedCommandId);

    /// <summary>The Unit first response frozen before the accepting append.</summary>
    public static async ValueTask<FirstResponseDescriptor> UnitResponseAsync(IImmutableContentStore content,
        Uuid tenantId, CancellationToken ct)
    {
        var result = await content.WriteJsonAsync(tenantId.ToString(), Uuid.CreateVersion4(), UnitResult.Schema,
            new UnitResult(), ct).ConfigureAwait(false);
        return new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType, UnitResult.Schema,
            new StandardResponseBody(result));
    }

    /// <summary>Checks domain guards before freezing the response, after native acceptance was resolved.</summary>
    public static async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(CommandFailure? failure,
        IImmutableContentStore content, Uuid tenantId, CancellationToken ct)
    {
        if (failure is not null)
            return Result<FirstResponseDescriptor>.Failure(failure.ToRequestError());
        return Result<FirstResponseDescriptor>.Success(await UnitResponseAsync(content, tenantId, ct)
            .ConfigureAwait(false));
    }

    /// <summary>The second half of an acceptance: the native fact was raised or the guard failed.</summary>
    public static Result Applied(CommandFailure? failure) =>
        failure is null ? Result.Success : Result.Failure(failure.ToRequestError());

    /// <summary>Maps the closed acceptance outcome to the command result.</summary>
    public static Result Done(Result<AcceptedAppendResult> accepted) => !accepted.IsSuccess
        ? Result.Failure(accepted.Error!)
        : accepted.Value switch
        {
            AcceptedAppendResult.Committed or AcceptedAppendResult.Replayed => Result.Success,
            _ => Result.Failure(AcceptedAppendResult.ConflictError),
        };

    /// <summary>The actor reference of the caller, or an error when the caller is not recognized.</summary>
    public static (ActorReference? Actor, RequestError? Error) ActorOf(System.Security.Claims.ClaimsPrincipal caller)
    {
        var actor = RegisterMemberHandler.ActorOf(caller);
        return actor is null
            ? (null, new RequestError(RequestErrorKind.Forbidden, "The caller identity is not recognized."))
            : (actor, null);
    }
}
