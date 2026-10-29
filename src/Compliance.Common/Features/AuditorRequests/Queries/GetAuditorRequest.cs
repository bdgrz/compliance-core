using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AuditorRequests.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.auditor-requests.get-auditor-request", 1)]
public sealed record GetAuditorRequest(Uuid TenantId, Uuid ProgramId, Uuid AuditorRequestId)
    : IRequest<AuditorRequestView>, IProgramReadRequest, ICallable;
