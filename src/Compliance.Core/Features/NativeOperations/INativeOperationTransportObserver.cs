using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>Observes a real committed/replayed native first response without reading or deciding domain authority.</summary>
public interface INativeOperationTransportObserver
{
    RequestError? ClassifyFailure(AcceptedOperationBinding binding, IExecutionContext context,
        ImmutableContentIntegrityException exception) => null;
    void Observe(AcceptedOperationBinding binding, IExecutionContext context, AcceptedAppendResult accepted);
}
