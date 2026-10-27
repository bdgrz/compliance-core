using Bdgrz.Compliance.Features.Changes.Aggregates;
using Bdgrz.Compliance.Features.Changes.Models;
namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Explicit verified read composition, separate from the compact command owner.</summary>
public sealed record SignificantChangeReadSnapshot(SignificantChange Owner, SignificantChangeRules Rules)
{
    public SignificantChangeView? View => Rules.Current;
}
