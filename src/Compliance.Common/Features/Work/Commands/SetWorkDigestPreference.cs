using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.Work.Commands;

/// <summary>Opts the acting member in to or out of the weekly email digest; in-app reminders always remain.</summary>
[Discriminator("bdgrz.work.digest_preference.set", 2)]
public sealed record SetWorkDigestPreference(Uuid CommandId, Uuid TenantId, bool EmailDigestEnabled,
    string? TimeZoneId = null)
    : IRequest<WorkDigestPreferenceView>, ITenantAccessRequest, ICallable, IIdentifiedRequest
{
    public Uuid RequestId => CommandId;
}
