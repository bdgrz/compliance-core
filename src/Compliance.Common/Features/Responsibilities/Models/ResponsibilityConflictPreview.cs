namespace Bdgrz.Compliance.Features.Responsibilities.Models;

public sealed record ResponsibilityConflictPreview(ResponsibilityScope Scope, long Revision,
    IReadOnlyList<ResponsibilityConflictView> Conflicts);
