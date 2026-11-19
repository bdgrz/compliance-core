using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Small fixed-edition provenance; entries and support gaps remain separate facts.</summary>
sealed record CriteriaSeedHeader(Uuid EditionId, string Framework, string EditionLabel,
    DateTimeOffset PublishedAt, bool NumberedCriteriaComplete, string CoverageNote, string SourceUrl,
    string ContentRights, string SeedVersion, string SeedFingerprint);
