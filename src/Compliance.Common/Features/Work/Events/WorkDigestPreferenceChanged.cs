using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Work.Events;

[Discriminator("bdgrz.work.digest_preference.changed", 1)]
public sealed record WorkDigestPreferenceChanged(Uuid TenantId, Uuid MemberId,
    bool EmailDigestEnabled, ActorReference ChangedBy, DateTimeOffset ChangedAt) : DomainEvent
{
    /// <summary>Validated member time zone; older persisted events default to UTC.</summary>
    public string TimeZoneId { get; init; } = "UTC";
}
