using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Verified bytes supplied as transient membership/absence evidence to one calendar preparation.</summary>
public sealed record TypeIICalendarHistoryRead(ImmutableContentRef Reference, TypeIICalendarHistoryNode Node);
