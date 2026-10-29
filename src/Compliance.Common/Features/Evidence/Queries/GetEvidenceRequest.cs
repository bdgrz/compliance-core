using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Evidence.Queries;

[Discriminator("bdgrz.evidence.request.get", 1)]
public sealed record GetEvidenceRequest(Uuid TenantId, Uuid ProgramId, Uuid EvidenceRequestId)
    : IRequest<EvidenceRequestView>, IProgramReadRequest, ICallable;
