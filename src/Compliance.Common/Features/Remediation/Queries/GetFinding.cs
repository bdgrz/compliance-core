using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Remediation.Queries;

[Discriminator("bdgrz.finding.get", 1)]
public sealed record GetFinding(Uuid TenantId, Uuid ProgramId, Uuid FindingId)
    : IRequest<FindingView>, IProgramReadRequest, ICallable;
