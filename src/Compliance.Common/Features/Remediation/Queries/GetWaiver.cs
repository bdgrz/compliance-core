using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Remediation.Queries;

[Discriminator("bdgrz.waiver.get", 1)]
public sealed record GetWaiver(Uuid TenantId, Uuid WaiverId) : IRequest<WaiverView>, ITenantAccessRequest, ICallable;
