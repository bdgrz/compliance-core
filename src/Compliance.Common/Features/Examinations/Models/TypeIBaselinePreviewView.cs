using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>Exact source manifest and blockers shown before Type I baseline confirmation.</summary>
public sealed record TypeIBaselinePreviewView(Uuid TenantId, Uuid ProgramId,
    Uuid ExaminationEngagementId, long EngagementRevision, DateOnly AsOf,
    IReadOnlyList<TypeIBaselineSourceReference> Sources,
    IReadOnlyList<TypeIBaselineSourceCoverage> SourceCoverage,
    IReadOnlyList<string> Blockers, IReadOnlyList<string> ScopeDifferences,
    string CanonicalManifest, string ContentSha256);
