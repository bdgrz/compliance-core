using Cntryl.Portia;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Services;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Models;
namespace Bdgrz.Compliance.Features.SystemDescription.Handlers;

public sealed class CreateSystemDescriptionDraftHandler(SystemDescriptionOperations operations, SystemDescriptionSourceResolver sources)
    : IRequestHandler<CreateSystemDescriptionDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<CreateSystemDescriptionDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.DescriptionId, request.DescriptionId,
            nameof(CreateSystemDescriptionDraft), null, async (snapshot, operation, token) =>
            {
                var resolved = await sources.ResolveAsync(request.TenantId, request.ProgramId, request.ExaminationId, null, token).ConfigureAwait(false);
                if (!resolved.IsSuccess)
                    return Result.Failure(resolved.Error!);
                var exact = resolved.Value;
                snapshot.PreparedSourceSnapshotContent = exact.Proof;
                var member = Uuid.Parse(operation.Actor.Id, System.Globalization.CultureInfo.InvariantCulture);
                return SystemDescriptionOperations.Outcome(snapshot.Rules.Create(Uuid.CreateVersion5(request.DescriptionId, "initial-version"),
                    exact.Scope, new SystemDescriptionDraftContent(request.Title, request.Sections, exact.Sources, [], []), member,
                    operation.Actor.Display, operation.AcceptedAt));
            }, null, ct);
    }
}
