using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Projections;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Projections;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Operations.Models;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>Loads the control and operating state an operations request acts on.</summary>
static class ControlOperationsSource
{
    public static async ValueTask<ControlReadSnapshot?> LoadControlAsync(ControlReader controlSources,
        Uuid tenantId, Uuid programId, Uuid controlId, CancellationToken ct)
    {
        var control = await controlSources.ReadAsync(tenantId, programId, controlId, ct)
            .ConfigureAwait(false);
        return control.IsVisible && control.ProgramId == programId ? control : null;
    }

    /// <summary>
    ///     Each visible planned control's current plan and its recorded and expected occurrences
    ///     starting on or before the horizon.
    /// </summary>
    public static async ValueTask<IReadOnlyList<PlannedControlOccurrences>> ReadPlannedAsync(
        ControlReader controlSources, ControlOperationsSnapshot snapshot, Uuid tenantId, Uuid programId,
        DateOnly today, DateOnly horizon, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var planned = new List<PlannedControlOccurrences>();
        foreach (var controlId in snapshot.PlannedControlIds.ToArray())
        {
            if (snapshot.CurrentPlan(controlId) is not { } plan)
                continue;
            var control = await LoadControlAsync(controlSources, tenantId, programId, controlId, ct)
                .ConfigureAwait(false);
            if (control is null)
                continue;
            planned.Add(new PlannedControlOccurrences(controlId, plan,
                control.ApprovedVersion?.Identifier ?? controlId.ToString(),
                snapshot.ReadOccurrences(controlId, OperatingAuthority.VersionWindows(control),
                    today, horizon)));
        }
        return planned;
    }

    /// <summary>The episode of every first-owner operating operation.</summary>
    public const string InitialEpisode = "initial";

    /// <summary>The provenance of a root operating operation on its own owner stream.</summary>
    public static OperationProvenance Provenance(Uuid logicalId, string commandKind, EventStreamAddress owner,
        OperationsActor actor, long expectedRevision, string intentDigest, DateTimeOffset acceptedAt) => new(
        logicalId, logicalId, null, commandKind, ActorReference.ForMember(actor.MemberId, actor.Display), owner,
        InitialEpisode, (ulong)expectedRevision, intentDigest, acceptedAt, owner, owner, owner);

    public static RequestError ControlNotFound() =>
        new(RequestErrorKind.NotFound, "The control was not found.");

    public static RequestError OccurrenceNotFound() =>
        new(RequestErrorKind.NotFound, "The control occurrence was not found.");

    public static DateOnly Today(TimeProvider clock) =>
        DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    /// <summary>
    ///     Resolves the performer of record. The owner or backup owner, directly or through a team,
    ///     performs as themselves; a workforce person holder's off-product performance may be
    ///     recorded by such a holder or a program manager, attributing both.
    /// </summary>
    public static async ValueTask<Result<OperatingHolder>> ResolvePerformerAsync(
        OperatingAuthority authority, Uuid tenantId, Uuid programId,
        ControlOperatingPlanView plan, OperationsActor actor, Uuid? personId, CancellationToken ct)
    {
        var holds = await authority.HoldsAsync(tenantId, plan.Owner, actor.MemberId, ct)
                        .ConfigureAwait(false) ||
                    await authority.HoldsAsync(tenantId, plan.BackupOwner, actor.MemberId, ct)
                        .ConfigureAwait(false);
        if (personId is { } person)
        {
            var personHolder = new OperatingHolder(OperatingAuthority.PersonHolder, person);
            if (plan.Owner != personHolder && plan.BackupOwner != personHolder)
                return Result<OperatingHolder>.Failure(new RequestError(RequestErrorKind.Validation,
                    "The performer person must hold the control's owner or backup owner responsibility."));
            return holds || await authority.ManagesProgramAsync(tenantId, actor, programId, ct)
                .ConfigureAwait(false)
                ? Result<OperatingHolder>.Success(personHolder)
                : Result<OperatingHolder>.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "Only an operating holder or a program manager may record a person's performance."));
        }
        return holds
            ? Result<OperatingHolder>.Success(new OperatingHolder(OperatingAuthority.MemberHolder,
                actor.MemberId))
            : Result<OperatingHolder>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the control owner or backup owner may perform and attest to this occurrence."));
    }
}
