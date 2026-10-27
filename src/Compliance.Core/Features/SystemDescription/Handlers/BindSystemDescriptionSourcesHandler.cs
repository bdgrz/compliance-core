using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

public sealed class BindSystemDescriptionSourcesHandler(SystemDescriptionOperations operations, SystemDescriptionSourceResolver sources)
    : IRequestHandler<BindSystemDescriptionSources>
{
    public ValueTask<Result> HandleAsync(IRequestContext<BindSystemDescriptionSources> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.DescriptionId, request.CommandId,
            nameof(BindSystemDescriptionSources), request.ExpectedRevision, async (snapshot, operation, token) =>
            {
                if (request.SourceVersionIds is null || request.SourceVersionIds.Count == 0 || request.SourceVersionIds.Any(id => id == Uuid.Empty) ||
                    request.SourceVersionIds.Distinct().Count() != request.SourceVersionIds.Count)
                    return Result.Failure(new RequestError(RequestErrorKind.Conflict, "A source refresh requires a nonempty unique source-version population."));
                var current = snapshot.CurrentVersion!;
                var resolved = await sources.ResolveAsync(request.TenantId, request.ProgramId, current.Scope.ExaminationId, current.Scope, token).ConfigureAwait(false);
                if (!resolved.IsSuccess)
                    return Result.Failure(resolved.Error!);
                var exact = resolved.Value;
                var requested = request.SourceVersionIds.ToHashSet();
                if (exact.Sources.Count != requested.Count || !requested.SetEquals(exact.Sources.Select(item => item.VersionId)))
                    return Result.Failure(new RequestError(RequestErrorKind.Conflict, "Every complete frozen source version must resolve exactly once."));
                snapshot.PreparedSourceSnapshotContent = exact.Proof;
                var member = Uuid.Parse(operation.Actor.Id, System.Globalization.CultureInfo.InvariantCulture);
                return SystemDescriptionOperations.Outcome(snapshot.Rules.BindSources(request.ExpectedRevision, request.CommandId,
                    exact.Sources, member, operation.Actor.Display, operation.AcceptedAt));
            }, null, ct);
    }
}
