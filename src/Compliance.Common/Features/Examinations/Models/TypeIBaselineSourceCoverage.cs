namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Commits to a complete scan of one baseline source family, including an empty scan.</summary>
public sealed record TypeIBaselineSourceCoverage(string Family, int RecordCount,
    string ContentSha256);
