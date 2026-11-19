using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Exact compiled facts permitted for the one built-in seed release.</summary>
/// <remarks>
/// These checks compare a request with compiled seed data. They do not prove the
/// supplied edition binding exists in source, authorize a caller, or establish
/// source coverage. Trusted bootstrap receivers must verify those separately.
/// </remarks>
static class PlatformCriteriaSeedDefinition
{
    public static Uuid OperationOf(Uuid editionId, string step) =>
        Uuid.CreateVersion5(editionId, "criteria-seed:" + CriteriaSeedFingerprint.Version + ":" + step);

    public static bool MatchesProposal(CriterionSeedProposed proposal, bool requireClaim = true)
    {
        var intent = proposal.Intent;
        if (intent is null || !CriteriaContentBounds.Identifier(intent.Identifier) ||
            !MatchesBinding(intent.EditionId, proposal.SeedVersion, proposal.SeedFingerprint) ||
            proposal.ExecutionGeneration <= 0 || proposal.EditionRequestEventId == Uuid.Empty ||
            (requireClaim && proposal.OperationClaimEventId == Uuid.Empty) ||
            intent.DraftGeneration != OperationOf(intent.EditionId, "edition") ||
            intent.OperationId != OperationOf(intent.EditionId, "entry:" + intent.Identifier) ||
            intent.ExpectedRevision != 0 || intent.Action != "create" ||
            intent.Kind is not ("criterion" or "point_of_focus") || intent.Category is null || intent.Summary is null ||
            proposal.IncarnationId != CriteriaEntry.IncarnationOf(intent.EditionId, intent.Identifier, intent.OperationId))
            return false;
        var entry = new Criterion(intent.EditionId, intent.Identifier, intent.Kind == "criterion" ? intent.Identifier : null,
            intent.Category, intent.Kind, intent.ParentIdentifier, intent.Summary);
        if (!MatchesEntry(intent.EditionId, proposal.SeedVersion, proposal.SeedFingerprint, entry))
            return false;
        return intent.Kind == "criterion"
            ? intent.ParentIdentifier is null && intent.ParentIncarnationId is null && proposal.ParentAcceptedEventId is null
            : intent.ParentIdentifier is { Length: > 0 } parent &&
                intent.ParentIncarnationId == CriteriaEntry.IncarnationOf(intent.EditionId, parent,
                    OperationOf(intent.EditionId, "entry:" + parent)) &&
                proposal.ParentAcceptedEventId is { } accepted && accepted != Uuid.Empty;
    }

    static readonly Lazy<string> CompiledFingerprint = new(() =>
        CriteriaSeedFingerprint.Compute(PlatformCriteriaCatalog.SeedEdition(),
            PlatformCriteriaCatalog.EnumerateSeedEntries()));

    public static bool MatchesBinding(Uuid editionId, string? version, string? fingerprint) =>
        editionId == PlatformCriteriaCatalog.SeedEdition().EditionId &&
        string.Equals(version, CriteriaSeedFingerprint.Version, StringComparison.Ordinal) &&
        string.Equals(fingerprint, CompiledFingerprint.Value, StringComparison.Ordinal);

    public static bool MatchesHeader(CriteriaSeedHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        var expected = PlatformCriteriaCatalog.SeedEdition();
        return MatchesBinding(header.EditionId, header.SeedVersion, header.SeedFingerprint) &&
            string.Equals(header.Framework, expected.Framework, StringComparison.Ordinal) &&
            string.Equals(header.EditionLabel, expected.EditionLabel, StringComparison.Ordinal) &&
            header.PublishedAt == expected.PublishedAt &&
            header.NumberedCriteriaComplete == expected.IsComplete &&
            string.Equals(header.CoverageNote, expected.CoverageNote, StringComparison.Ordinal) &&
            string.Equals(header.SourceUrl, expected.SourceUrl, StringComparison.Ordinal) &&
            string.Equals(header.ContentRights, expected.ContentRights, StringComparison.Ordinal);
    }

    public static bool MatchesEntry(Uuid editionId, string? version, string? fingerprint, Criterion entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!MatchesBinding(editionId, version, fingerprint))
            return false;

        foreach (var expected in PlatformCriteriaCatalog.EnumerateSeedEntries())
            if (string.Equals(expected.Identifier, entry.Identifier, StringComparison.Ordinal))
                return expected == entry;
        return false;
    }

    public static bool MatchesDisclosure(Uuid editionId, string? version, string? fingerprint,
        CriteriaSupportGap disclosure)
    {
        ArgumentNullException.ThrowIfNull(disclosure);
        if (!MatchesBinding(editionId, version, fingerprint))
            return false;

        foreach (var expected in PlatformCriteriaCatalog.SeedEdition().SupportGaps)
            if (string.Equals(expected.Category, disclosure.Category, StringComparison.Ordinal) &&
                string.Equals(expected.Code, disclosure.Code, StringComparison.Ordinal))
                return expected == disclosure;
        return false;
    }
}
