using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>The normalized terms an evidence request is opened with.</summary>
public sealed record EvidenceRequestTerms(string Title, string Instructions, Uuid OwnerMemberId, DateOnly DueOn,
    Uuid? ControlId, ActorReference RequestedBy, DateTimeOffset OpenedAt);
