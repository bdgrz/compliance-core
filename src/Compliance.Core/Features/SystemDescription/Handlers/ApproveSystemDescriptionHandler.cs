using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Services;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Commands;
namespace Bdgrz.Compliance.Features.SystemDescription.Handlers;

public sealed class ApproveSystemDescriptionHandler(SystemDescriptionOperations operations) : IRequestHandler<ApproveSystemDescription>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ApproveSystemDescription> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!SystemDescriptionCommandActor.TryGet(context.Actor, request.TenantId, out var member, out var display))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Unauthorized, "A description decision requires a Bdgrz member identity.")));
        return operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.DescriptionId, request.CommandId,
            nameof(ApproveSystemDescription), request.ExpectedRevision, (snapshot, operation, token) =>
            {
                var qualification = SystemDescriptionApprovalQualification.Check(snapshot);
                if (!qualification.IsSuccess)
                    return ValueTask.FromResult(qualification);
                return ValueTask.FromResult(SystemDescriptionOperations.Outcome(snapshot.Rules.Approve(request.Version, member, display, request.Rationale, operation.AcceptedAt)));
            }, (rules, operation) => null, ct);
    }
}
