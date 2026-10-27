using Cntryl.Portia;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.Work.Queries;

/// <summary>Reads a member's durable weekly digest delivery status for one tenant-local record.</summary>
[Discriminator("bdgrz.work.digest.dispatch.status.get", 1)]
public sealed record GetWorkDigestDispatchStatus(Uuid TenantId, Uuid MemberId, DateOnly WeekOf)
    : IRequest<WorkDigestDispatchStatusView>, ICallable, IPlatformOperatorRequest;
