using Bdgrz.Compliance.Features.Examinations.Models;
namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>The exact references and unresolved conditions for one covered source family.</summary>
public sealed record TypeIBaselineSourceContribution(string Family,
    IReadOnlyList<TypeIBaselineSourceReference> Sources,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> ScopeDifferences);
