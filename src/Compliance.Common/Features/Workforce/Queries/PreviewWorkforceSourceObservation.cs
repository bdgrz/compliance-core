using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Features.Workforce.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Queries;

/// <summary>Previews source authority and conflicts; never applies facts or reveals restricted values.</summary>
[Discriminator("bdgrz.workforce.source.preview", 1)]
public sealed record PreviewWorkforceSourceObservation(Uuid TenantId, Uuid ObservationId)
    : IRequest<WorkforceSourcePreview>, IWorkforceRequest, ICallable;
