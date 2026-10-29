using Cntryl.Portia;
using Bdgrz.Compliance.Features.SystemDescription.Services;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Commands;
namespace Bdgrz.Compliance.Features.SystemDescription.Handlers;

public sealed class ProposeSystemDescriptionSuccessorHandler(SystemDescriptionOperations operations) : IRequestHandler<ProposeSystemDescriptionSuccessor>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ProposeSystemDescriptionSuccessor> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.DescriptionId, request.CommandId,
            nameof(ProposeSystemDescriptionSuccessor), request.ExpectedRevision, (snapshot, operation, token) =>
            {
                var member = Uuid.Parse(operation.Actor.Id, System.Globalization.CultureInfo.InvariantCulture);
                return ValueTask.FromResult(SystemDescriptionOperations.Outcome(snapshot.Rules.ProposeSuccessor(request.ExpectedRevision,
                    request.CommandId, snapshot.CurrentVersion!.Content, request.ChangeReason, member, operation.Actor.Display, operation.AcceptedAt)));
            }, null, ct);
    }
}
