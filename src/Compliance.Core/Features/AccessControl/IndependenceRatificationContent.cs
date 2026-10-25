using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>One immutable personal decision linked to its exact previous monotonic activation.</summary>
public sealed record IndependenceRatificationContent(EventStreamAddress Owner, Uuid RequestId, long Sequence,
    IndependenceRuleRatificationView Ratification, ImmutableContentRef? Previous);
