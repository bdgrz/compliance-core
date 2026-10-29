using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Models;
using Bdgrz.Compliance.Features.SystemDescription.Services;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Commands;
namespace Bdgrz.Compliance.Features.SystemDescription.Handlers;

public sealed class RecordSystemDescriptionNoChangeDecisionHandler(SystemDescriptionOperations operations) : IRequestHandler<RecordSystemDescriptionNoChangeDecision>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordSystemDescriptionNoChangeDecision> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!SystemDescriptionCommandActor.TryGet(context.Actor, request.TenantId, out var member, out var display))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Unauthorized, "A description decision requires a Bdgrz member identity.")));
        return operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.DescriptionId, request.CommandId,
            nameof(RecordSystemDescriptionNoChangeDecision), request.ExpectedRevision, (snapshot, operation, token) =>
            {
                return ValueTask.FromResult(SystemDescriptionOperations.Outcome(snapshot.Rules.RecordNoChangeDecision(request.Version, request.ChangeId, request.Rationale, member, display, operation.AcceptedAt)));
            }, (rules, operation) => new SystemDescriptionNoChangeRecordedBody(request.TenantId, request.ProgramId, request.DescriptionId, rules.Revision, rules.NoChangeDecisions.First(item => item.Version == request.Version && item.ChangeId == request.ChangeId), operation.AcceptedAt), ct);
    }
}
