using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>One complete immutable authored version and its exact previous catalog cut.</summary>
public sealed record IndependenceRuleVersionContent(EventStreamAddress Owner, Uuid RequestId, long Sequence,
    IndependenceRuleVersionView Version, ImmutableContentRef? Previous);
