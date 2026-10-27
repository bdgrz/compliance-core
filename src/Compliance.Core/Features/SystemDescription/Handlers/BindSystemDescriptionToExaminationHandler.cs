using Bdgrz.Compliance.Features.SystemDescription;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription.Handlers;

public sealed class BindSystemDescriptionToExaminationHandler(SystemDescriptionOperations operations) : IRequestHandler<BindSystemDescriptionToExamination>
{
    public ValueTask<Result> HandleAsync(IRequestContext<BindSystemDescriptionToExamination> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.DescriptionId, request.CommandId,
            nameof(BindSystemDescriptionToExamination), request.ExpectedRevision, (snapshot, operation, token) =>
            {
                var version = snapshot.FindVersion(request.Version);
                var id = version?.Scope.Stage switch
                {
                    "type_i" => version.Scope.TypeIBaselineId ?? Uuid.Empty,
                    "type_ii" => version.Scope.CloseSnapshotId ?? Uuid.Empty,
                    _ => Uuid.Empty,
                };
                var member = Uuid.Parse(operation.Actor.Id, System.Globalization.CultureInfo.InvariantCulture);
                return ValueTask.FromResult(SystemDescriptionOperations.Outcome(snapshot.Rules.BindToExamination(request.Version,
                    request.ExaminationId, request.BindingPurpose, id, member, operation.Actor.Display, operation.AcceptedAt)));
            }, (rules, operation) => new SystemDescriptionBoundBody(request.TenantId, request.ProgramId, request.DescriptionId, rules.Revision,
                rules.Read().Bindings.First(item => item.Version == request.Version && item.Purpose == request.BindingPurpose), operation.AcceptedAt), ct);
    }
}
