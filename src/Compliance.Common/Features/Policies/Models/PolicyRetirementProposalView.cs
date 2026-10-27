using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.Policies.Models;

public sealed record PolicyRetirementProposalView(long Version, DateOnly EffectiveUntil,
    string Rationale, ActorReference ProposedBy, DateTimeOffset ProposedAt);
