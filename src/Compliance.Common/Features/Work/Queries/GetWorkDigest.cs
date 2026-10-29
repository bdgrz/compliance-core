using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.Work.Queries;

/// <summary>The acting member's weekly digest: their overdue items and items due in the next 7 days.</summary>
[Discriminator("bdgrz.work.digest.get", 1)]
public sealed record GetWorkDigest(Uuid TenantId, Uuid ProgramId)
    : IRequest<WorkDigestView>, IProgramReadRequest, ICallable;
