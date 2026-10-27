using Cntryl.Portia;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Controls.Queries;

[Discriminator("bdgrz.control.version.current.get", 1)]
public sealed record GetCurrentControlVersion(Uuid TenantId, Uuid ProgramId, Uuid ControlId)
    : IRequest<ControlVersionView>, IProgramScopedRequest, ICallable;
