using Bdgrz.Compliance.Features.AuditPopulations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

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
