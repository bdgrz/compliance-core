using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Operations.Readers;
using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Resolves the occurrence, its plan, and the performer of record before an attestation.</summary>
static class OccurrenceAttestation
{
    public static async ValueTask<Result<PreparedOccurrenceProof>> PrepareAsync(ControlReader controlSources,
        ControlOperationsReader operations, OperatingAuthority authority, Uuid tenantId, Uuid programId,
        Uuid controlId, Uuid occurrenceId, Uuid? personId, ClaimsPrincipal principal, DateTimeOffset now,
        CancellationToken ct)
    {
        var control = await ControlOperationsSource.LoadControlAsync(controlSources, tenantId, programId, controlId, ct)
            .ConfigureAwait(false);
        if (control is null)
            return Failure(ControlOperationsSource.ControlNotFound());
        var windows = OperatingAuthority.VersionWindows(control);
        var snapshot = await operations.ReadOccurrenceAsync(tenantId, programId, controlId, occurrenceId, ct)
            .ConfigureAwait(false);
        if (snapshot.ReadOccurrence(controlId, occurrenceId, windows, DateOnly.FromDateTime(now.UtcDateTime)) is not
            { } occurrence || snapshot.FindPlan(controlId, occurrence.PlanVersionId) is not { } plan)
            return Failure(ControlOperationsSource.OccurrenceNotFound());
        // Ownership is evaluated against the plan now in force, so reassignment moves who may act.
        var governing = snapshot.CurrentPlan(controlId) ?? plan;
        var actor = OperationsActor.From(principal, tenantId);
        var performer = await ControlOperationsSource.ResolvePerformerAsync(authority, tenantId, programId, governing,
            actor, personId, ct).ConfigureAwait(false);
        return performer.IsSuccess
            ? Result<PreparedOccurrenceProof>.Success(new PreparedOccurrenceProof(actor, performer.Value, windows,
                snapshot, occurrence, plan))
            : Failure(performer.Error!);
    }

    static Result<PreparedOccurrenceProof> Failure(RequestError error) =>
        Result<PreparedOccurrenceProof>.Failure(error);
}
