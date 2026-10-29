using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.Applications.Projections;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.Applications.Services;

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
