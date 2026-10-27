using Bdgrz.Compliance.Features.Applications.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Shared rules of omission proposals: the proposed row of a missing claim and the alias guards.</summary>
static class ApplicationImportOmissions
{
    public static ApplicationImportRetirementRow RetirementRow(Uuid sourceId, ApplicationImportSourceClaimRecord claim) =>
        new(ApplicationImportEffects.SourceClaimId(sourceId, claim.Observation.SourceRecordId),
            claim.Observation.SourceRecordId, claim.Observation.ApplicationId, claim.LastObservedBatchId,
            claim.Observation.ExpectedApplicationRevision ?? 1, claim.CommittedAt);

    /// <summary>
    ///     A target shared by several claims, or a target a present row of this batch is correlated to, cannot be
    ///     retired by an omission: the omitted alias is not the only observation of that application.
    /// </summary>
    public static bool HasSharedOrPresentAlias(ApplicationImportBatchHistorySnapshot history, ApplicationImportSourceView view,
        IReadOnlyList<ApplicationImportSourceClaimRecord> missing, IReadOnlyList<ApplicationImportStagedRow> rows)
    {
        if (missing.Any(claim => view.Claims.Count(other => other.Observation.ApplicationId ==
                claim.Observation.ApplicationId) != 1))
            return true;
        var targets = missing.Select(claim => claim.Observation.ApplicationId).ToHashSet();
        var present = rows.Select(row => row.SourceRecordId).ToHashSet(StringComparer.Ordinal);
        return rows.Any(row => present.Contains(row.SourceRecordId) &&
            history.GetCorrelation(row.RowId) is { } choice && targets.Contains(choice.ApplicationId));
    }
}
