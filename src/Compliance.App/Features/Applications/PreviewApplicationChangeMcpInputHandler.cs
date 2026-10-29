using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.Applications;
namespace Bdgrz.Compliance.Features.Applications;

public sealed class PreviewApplicationChangeMcpInputHandler(IRequestBus bus)
    : IRequestHandler<PreviewApplicationChangeMcpInput, ApplicationChangePreview>
{
    public ValueTask<Result<ApplicationChangePreview>> HandleAsync(
        IRequestContext<PreviewApplicationChangeMcpInput> context, CancellationToken ct)
    {
        var input = context.Request;
        var request = new PreviewApplicationChange(input.TenantId, input.ApplicationId,
            input.ExpectedApplicationRevision, input.ChangeKind, input.Name, input.Purpose,
            input.OwnerReference, input.Classification, input.SystemOwnerPersonId,
            input.AccessOwnerPersonId, input.ApplicationKind, input.Criticality)
        {
            ClassificationIntent = input.ClassificationIntent,
            SystemOwnerIntent = input.SystemOwnerIntent,
            AccessOwnerIntent = input.AccessOwnerIntent,
        };
        // The original request retains its full authorization and aggregate execution pipeline.
        return bus.DispatchAsync(request, (RequestDispatchContext)context, ct);
    }
}
