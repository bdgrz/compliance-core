using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.Work.Queries;

/// <summary>Reads the acting member's weekly email digest preference.</summary>
[Discriminator("bdgrz.work.digest_preference.get", 1)]
public sealed record GetWorkDigestPreference(Uuid TenantId)
    : IRequest<WorkDigestPreferenceView>, ITenantAccessRequest, ICallable;
