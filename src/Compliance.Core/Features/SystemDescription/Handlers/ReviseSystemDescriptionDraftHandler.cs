using Cntryl.Portia;
using Bdgrz.Compliance.Features.SystemDescription.Services;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Models;
namespace Bdgrz.Compliance.Features.SystemDescription.Handlers;

public sealed class ReviseSystemDescriptionDraftHandler(SystemDescriptionOperations operations) : IRequestHandler<ReviseSystemDescriptionDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseSystemDescriptionDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.DescriptionId, request.CommandId,
            nameof(ReviseSystemDescriptionDraft), request.ExpectedRevision, (snapshot, operation, token) =>
            {
                var current = snapshot.CurrentVersion!;
                var value = new SystemDescriptionDraftContent(request.Title, request.Sections, current.Content.Sources,
                    current.Content.PeriodChanges, current.Content.SignificantChangeIds);
                var member = Uuid.Parse(operation.Actor.Id, System.Globalization.CultureInfo.InvariantCulture);
                return ValueTask.FromResult(SystemDescriptionOperations.Outcome(snapshot.Rules.Revise(request.ExpectedRevision,
                    request.CommandId, value, request.ChangeReason, member, operation.Actor.Display, operation.AcceptedAt)));
            }, null, ct);
    }
}
