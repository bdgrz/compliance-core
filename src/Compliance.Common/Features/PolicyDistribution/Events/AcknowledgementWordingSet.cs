using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Events;

/// <summary>One attributed organization wording choice; earlier choices remain in history.</summary>
[Discriminator("bdgrz.policy.acknowledgement-wording-set", 1)]
public sealed record AcknowledgementWordingSet(Uuid TenantId, long Revision, Uuid CommandId,
    long OriginalExpectedRevision, string AuthoredIntentSha256, string Wording,
    ActorReference SetBy, DateTimeOffset SetAt) : DomainEvent;
