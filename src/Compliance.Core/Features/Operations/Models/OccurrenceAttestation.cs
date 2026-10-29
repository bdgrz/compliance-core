using System.Security.Claims;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Operations.Models;
namespace Bdgrz.Compliance.Features.Operations.Models;

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
