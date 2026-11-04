using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence.Models;

/// <summary>The attributed terminal withdrawal of an evidence support assertion.</summary>
public sealed record EvidenceSupportWithdrawalView(Uuid CommandId, string Reason, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset WithdrawnAt);
