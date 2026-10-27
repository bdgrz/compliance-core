using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Transient verified membership/absence evidence; never retained by the command owner.</summary>
public sealed record IndependenceRatificationHistoryRead(ImmutableContentRef Reference, IndependenceRatificationContent Content);
