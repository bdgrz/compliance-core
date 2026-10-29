using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
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
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AuditPopulations.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AuditorRequests.Readers;

/// <summary>Resolves a sample against one immutable, eligible population version.</summary>
static class SampleSelectionPopulationSourceReader
{
    public static async ValueTask<Result<AuditPopulationVersionView>> LoadAsync(
        AuditPopulationReader populations, Uuid tenantId, Uuid programId, Uuid examinationId,
        Uuid populationId, int populationVersion, IReadOnlyList<Uuid> selectedRowIds,
        CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || examinationId == Uuid.Empty ||
            populationId == Uuid.Empty || populationVersion <= 0)
            return Invalid("A sample selection must identify its examination and frozen population version.");

        var population = await populations.ReadAsync(tenantId, programId, populationId, ct).ConfigureAwait(false);
        if (population.View is not { } current || current.TenantId != tenantId ||
            current.ProgramId != programId || current.AuditPopulationId != populationId)
            return Result<AuditPopulationVersionView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The audit population was not found in this program."));
        if (current.ExaminationId != examinationId)
            return Result<AuditPopulationVersionView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The audit population was not found for this examination."));

        var version = current.Versions.FirstOrDefault(item => item.Version == populationVersion);
        if (version is null)
            return Result<AuditPopulationVersionView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The frozen audit population version was not found."));
        if (!version.SampleEligible)
            return Result<AuditPopulationVersionView>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The selected population version is not eligible for sampling."));
        if (selectedRowIds is null || selectedRowIds.Count is 0 or > 500)
            return Invalid("A sample selection requires between one and 500 population rows.");

        var includedRows = version.Rows.Where(static row => row.Classification == "included")
            .Select(static row => row.RowId).ToHashSet();
        if (selectedRowIds.Any(rowId => rowId == Uuid.Empty || !includedRows.Contains(rowId)))
            return Invalid("Every selected row must be included in the exact frozen population version.");

        return Result<AuditPopulationVersionView>.Success(version);
    }

    static Result<AuditPopulationVersionView> Invalid(string message) =>
        Result<AuditPopulationVersionView>.Failure(new RequestError(RequestErrorKind.Validation, message));
}
