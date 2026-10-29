using Cntryl.Portia;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Controls.Queries;

[Discriminator("bdgrz.control.version.effective.get", 1)]
public sealed record GetEffectiveControlVersion(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    DateOnly EffectiveOn)
    : IRequest<ControlVersionView>, IProgramScopedRequest, ICallable;
