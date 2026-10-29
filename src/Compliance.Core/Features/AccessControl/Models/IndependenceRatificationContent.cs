using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>One immutable personal decision linked to its exact previous monotonic activation.</summary>
public sealed record IndependenceRatificationContent(EventStreamAddress Owner, Uuid RequestId, long Sequence,
    IndependenceRuleRatificationView Ratification, ImmutableContentRef? Previous);
