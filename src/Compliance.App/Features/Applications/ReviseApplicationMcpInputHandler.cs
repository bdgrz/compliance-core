using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     Forwards the MCP shape to the one canonical <see cref="ReviseApplication" /> command: the command id is the
///     same typed logical identity on both, and the setter-based field presence is restored on the forwarded command.
/// </summary>
public sealed class ReviseApplicationMcpInputHandler(IRequestBus bus) : IRequestHandler<ReviseApplicationMcpInput>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseApplicationMcpInput> context, CancellationToken ct)
    {
        var input = context.Request;
        var request = new ReviseApplication(input.CommandId, input.TenantId, input.ApplicationId,
            input.ExpectedRevision, input.Name, input.Purpose, input.OwnerReference, input.Classification,
            input.SystemOwnerPersonId, input.AccessOwnerPersonId, input.IsRestricted, input.ApplicationKind,
            input.Criticality)
        {
            ClassificationIntent = input.ClassificationIntent,
            SystemOwnerIntent = input.SystemOwnerIntent,
            AccessOwnerIntent = input.AccessOwnerIntent,
        };
        // The original request retains its full authorization and aggregate execution pipeline.
        return bus.DispatchAsync(request, (RequestDispatchContext)context, ct);
    }
}
