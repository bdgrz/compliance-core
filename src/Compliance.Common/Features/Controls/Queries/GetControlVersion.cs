using Cntryl.Portia;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Controls.Queries;

[Discriminator("bdgrz.control.version.get", 1)]
public sealed record GetControlVersion(Uuid TenantId, Uuid ProgramId, Uuid ControlId, Uuid VersionId)
    : IRequest<ControlVersionView>, IProgramScopedRequest, ICallable;
