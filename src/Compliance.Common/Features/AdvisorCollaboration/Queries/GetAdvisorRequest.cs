using Cntryl.Portia;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.advisor-collaboration.get-advisor-request", 1)]
public sealed record GetAdvisorRequest(Uuid TenantId, Uuid ProgramId, Uuid AdvisorRequestId)
    : IRequest<AdvisorRequestView>, IProgramReadRequest, ICallable;
