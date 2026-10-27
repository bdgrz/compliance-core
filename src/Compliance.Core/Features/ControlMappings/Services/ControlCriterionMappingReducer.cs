using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.ControlMappings.Services;

/// <summary>
///     Folds one mapping fact into the mapping line's public view. The owner, its readers and the coverage projection
///     use it, so projected views match owner reads exactly.
/// </summary>
static class ControlCriterionMappingReducer
{
    public static ControlCriterionMappingView Apply(ControlCriterionMappingView? current,
        object domainEvent) => domainEvent switch
        {
            ControlCriterionMappingProposedChange proposed => WithStatus((current ??
                new ControlCriterionMappingView(proposed.TenantId, proposed.ProgramId,
                    proposed.MappingId, proposed.ControlId, proposed.EditionId,
                    proposed.CriterionIdentifier, proposed.CriterionKind, 0, "pending", null, null,
                    [])) with
            {
                Revision = proposed.Revision,
                Versions = [.. current?.Versions ?? [], new ControlCriterionMappingVersionView(
                proposed.VersionNumber, proposed.MappingId, proposed.ControlVersionId, "proposed",
                proposed.Rationale, proposed.ApplicabilityExplanation, proposed.Actor,
                proposed.ProposedAt, null, null, null, null, null, null, null, null)],
            }),
            ControlCriterionMappingReviewedChange reviewed => Reviewed(Require(current), reviewed),
            ControlCriterionMappingRetiredChange retired => WithStatus(Require(current) with
            {
                Revision = retired.Revision,
                ActiveVersionNumber = null,
                ActiveControlVersionId = null,
                Versions = [.. Require(current).Versions.Select(version =>
                version.VersionNumber == retired.VersionNumber
                    ? version with
                    {
                        Status = "retired",
                        RetiredBy = retired.Actor,
                        RetirementRationale = retired.Rationale,
                        RetiredAt = retired.RetiredAt,
                    }
                    : version)],
            }),
            _ => throw new ArgumentException("The event is not a control criterion mapping event.",
                nameof(domainEvent)),
        };

    static ControlCriterionMappingView Reviewed(ControlCriterionMappingView current,
        ControlCriterionMappingReviewedChange reviewed)
    {
        var accepted = reviewed.Outcome == "accept";
        var versions = current.Versions.Select(version =>
        {
            if (version.VersionNumber == reviewed.VersionNumber)
                return version with
                {
                    Status = accepted ? "accepted" : "rejected",
                    ReviewDecisionId = reviewed.DecisionId,
                    ReviewedBy = reviewed.Actor,
                    ReviewRationale = reviewed.Rationale,
                    ReviewedAt = reviewed.DecidedAt,
                    SeparationOfDutiesWaiverId = reviewed.SeparationOfDutiesWaiverId,
                };
            return accepted && version.VersionNumber == current.ActiveVersionNumber
                ? version with { Status = "superseded" }
                : version;
        }).ToArray();
        var active = accepted
            ? versions.Single(version => version.VersionNumber == reviewed.VersionNumber)
            : null;
        return WithStatus(current with
        {
            Revision = reviewed.Revision,
            ActiveVersionNumber = accepted ? reviewed.VersionNumber : current.ActiveVersionNumber,
            ActiveControlVersionId = accepted
                ? active!.ControlVersionId
                : current.ActiveControlVersionId,
            Versions = versions,
        });
    }

    static ControlCriterionMappingView WithStatus(ControlCriterionMappingView view) => view with
    {
        Status = view.Versions.Any(static version => version.Status == "proposed") ? "pending"
            : view.ActiveVersionNumber is not null ? "active"
            : view.Versions.Any(static version => version.Status == "retired") ? "retired"
            : "rejected",
    };

    static ControlCriterionMappingView Require(ControlCriterionMappingView? current) =>
        current ?? throw new InvalidOperationException(
            "A mapping decision cannot project before its proposal.");
}
