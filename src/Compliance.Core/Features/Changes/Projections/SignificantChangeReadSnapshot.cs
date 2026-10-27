using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Changes.Rules;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Changes.Aggregates;
using Bdgrz.Compliance.Features.Changes.Models;
namespace Bdgrz.Compliance.Features.Changes.Projections;

/// <summary>Explicit verified read composition, separate from the compact command owner.</summary>
public sealed record SignificantChangeReadSnapshot(SignificantChange Owner, SignificantChangeRules Rules)
{
    public SignificantChangeView? View => Rules.Current;
}
