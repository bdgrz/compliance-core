using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     The held proof of one gap plan: the exact plan fact the decision froze, or none for a gap that had no plan, and
///     the grant fact that holds it.
/// </summary>
public sealed record ReadinessPlanProof(Uuid GapId, NativeFactReference? Plan, NativeFactReference Grant);
