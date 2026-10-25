using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

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
