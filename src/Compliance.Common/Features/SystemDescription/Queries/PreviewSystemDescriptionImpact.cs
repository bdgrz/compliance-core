using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.SystemDescription.Models;
namespace Bdgrz.Compliance.Features.SystemDescription.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.system-description.preview-system-description-impact", 1)]
public sealed record PreviewSystemDescriptionImpact(Uuid TenantId, Uuid ProgramId, Uuid SystemDescriptionId)
    : IRequest<SystemDescriptionImpactPreview>, IProgramReadRequest, ICallable;
