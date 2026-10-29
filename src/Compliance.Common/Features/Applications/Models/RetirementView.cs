using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>An effective-dated retirement (M0-D05); a merge retains the approved successor snapshot revision.</summary>
public sealed record RetirementView(DateTimeOffset EffectiveAt, string Reason,
    Uuid? MergedIntoApplicationId, long? MergedIntoApplicationRevision = null);
