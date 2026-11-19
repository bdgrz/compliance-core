using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;

namespace Bdgrz.Compliance.Features.Evidence.Queries;

[Discriminator("bdgrz.evidence.support.get", 1)]
public sealed record GetEvidenceSupport(Uuid TenantId, Uuid ProgramId, Uuid EvidenceSupportId)
    : IRequest<EvidenceSupportStatusView>, IProgramReadRequest, ICallable;
