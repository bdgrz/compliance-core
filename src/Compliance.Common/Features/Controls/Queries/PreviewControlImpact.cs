using Cntryl.Portia;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Controls.Queries;

/// <summary>Previews the pending successor or retirement at one exact control revision.</summary>
[Discriminator("bdgrz.control.impact.preview", 1)]
public sealed record PreviewControlImpact(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long ExpectedRevision)
    : IRequest<ControlImpactPreview>, IProgramScopedRequest, ICallable;
