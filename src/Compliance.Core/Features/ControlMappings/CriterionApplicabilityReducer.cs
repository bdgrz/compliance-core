using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     Folds one applicability fact into the criterion line's public view. The owner, its readers and the coverage
///     projection use it, so projected views match owner reads exactly.
/// </summary>
static class CriterionApplicabilityReducer
{
    public static CriterionApplicabilityView Apply(CriterionApplicabilityView? current,
        object domainEvent) => domainEvent switch
        {
            CriterionNotApplicableProposedChange proposed => WithStatus((current ??
                new CriterionApplicabilityView(proposed.TenantId, proposed.ProgramId,
                    proposed.DecisionId, proposed.EditionId, proposed.CriterionIdentifier, 0,
                    "pending", null, [])) with
            {
                Revision = proposed.Revision,
                Versions = [.. current?.Versions ?? [], new CriterionApplicabilityVersionView(
                proposed.VersionNumber, proposed.DecisionId, "proposed", proposed.Rationale, proposed.Actor,
                proposed.ProposedAt, null, null, null, null, null, null, null, null)],
            }),
            CriterionApplicabilityReviewedChange reviewed => Reviewed(Require(current), reviewed),
            CriterionNotApplicableWithdrawnChange withdrawn => WithStatus(Require(current) with
            {
                Revision = withdrawn.Revision,
                ActiveVersionNumber = null,
                Versions = [.. Require(current).Versions.Select(version =>
                version.VersionNumber == withdrawn.VersionNumber
                    ? version with
                    {
                        Status = "withdrawn",
                        WithdrawnBy = withdrawn.Actor,
                        WithdrawalRationale = withdrawn.Rationale,
                        WithdrawnAt = withdrawn.WithdrawnAt,
                    }
                    : version)],
            }),
            _ => throw new ArgumentException("The event is not a criterion applicability event.",
                nameof(domainEvent)),
        };

    static CriterionApplicabilityView Reviewed(CriterionApplicabilityView current,
        CriterionApplicabilityReviewedChange reviewed)
    {
        var accepted = reviewed.Outcome == "accept";
        return WithStatus(current with
        {
            Revision = reviewed.Revision,
            ActiveVersionNumber = accepted ? reviewed.VersionNumber : current.ActiveVersionNumber,
            Versions = [.. current.Versions.Select(version =>
            {
                if (version.VersionNumber == reviewed.VersionNumber)
                    return version with
                    {
                        Status = accepted ? "accepted" : "rejected",
                        ReviewDecisionId = reviewed.ReviewDecisionId,
                        ReviewedBy = reviewed.Actor,
                        ReviewRationale = reviewed.Rationale,
                        ReviewedAt = reviewed.ReviewedAt,
                        SeparationOfDutiesWaiverId = reviewed.SeparationOfDutiesWaiverId,
                    };
                return accepted && version.VersionNumber == current.ActiveVersionNumber
                    ? version with { Status = "superseded" }
                    : version;
            })],
        });
    }

    static CriterionApplicabilityView WithStatus(CriterionApplicabilityView view) => view with
    {
        Status = view.Versions.Any(static version => version.Status == "proposed") ? "pending"
            : view.ActiveVersionNumber is not null ? "not_applicable"
            : view.Versions.Any(static version => version.Status == "withdrawn") ? "withdrawn"
            : "rejected",
    };

    static CriterionApplicabilityView Require(CriterionApplicabilityView? current) =>
        current ?? throw new InvalidOperationException(
            "An applicability decision cannot project before its proposal.");
}
