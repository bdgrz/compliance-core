using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Examinations.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.examinations.get-type-i-i-plan", 1)]
public sealed record GetTypeIIPlan(Uuid TenantId, Uuid ProgramId, Uuid TypeIIPlanId)
    : IRequest<TypeIIPlanView>, IProgramReadRequest, ICallable;
