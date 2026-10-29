using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.NativeOperations.Contracts;

/// <summary>Observes a real committed/replayed native first response without reading or deciding domain authority.</summary>
public interface INativeOperationTransportObserver
{
    RequestError? ClassifyFailure(AcceptedOperationBinding binding, IExecutionContext context,
        ImmutableContentIntegrityException exception) => null;
    void Observe(AcceptedOperationBinding binding, IExecutionContext context, AcceptedAppendResult accepted);
}
