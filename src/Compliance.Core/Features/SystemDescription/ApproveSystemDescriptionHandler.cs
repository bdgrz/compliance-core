using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

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
