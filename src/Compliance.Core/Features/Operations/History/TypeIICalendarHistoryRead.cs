using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Operations.History;

/// <summary>Verified bytes supplied as transient membership/absence evidence to one calendar preparation.</summary>
public sealed record TypeIICalendarHistoryRead(ImmutableContentRef Reference, TypeIICalendarHistoryNode Node);
