using Bdgrz.Compliance.Features.Retention.Aggregates;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Retention.Queries;
using Bdgrz.Compliance.Features.Retention.Readers;
using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention.Handlers;

sealed class ListArtifactLegalHoldsHandler(IAggregateReader reader, IDomainEventReader events)
    : IRequestHandler<ListArtifactLegalHolds, Page<ArtifactLegalHoldView>>
{
    public async ValueTask<Result<Page<ArtifactLegalHoldView>>> HandleAsync(
        IRequestContext<ListArtifactLegalHolds> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200 || request.MinimumRevision is < 1 || request.Cursor is { Length: > 256 })
            return Fail(RequestErrorKind.Validation, "The retention history paging parameters are invalid.");
        var source = await ArtifactRetentionSourceReader.LoadAsync(reader, request.TenantId,
            request.SourceKind, request.SourceId, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<Page<ArtifactLegalHoldView>>.Failure(source.Error!);
        var directory = new ArtifactRetentionReader(reader, events);
        var retention = await directory.ReadAsync(source.Value, ct).ConfigureAwait(false);
        if (!retention.IsSuccess)
            return Result<Page<ArtifactLegalHoldView>>.Failure(retention.Error!);
        if (request.MinimumRevision > retention.Value.Revision)
            return Fail(RequestErrorKind.Conflict, "The requested retention composition revision is not yet durable.", true);
        var identity = ArtifactRetentionBasis.IdentityOf(source.Value.Source);
        var prefix = $"retention_holds_v2:{identity}:{retention.Value.Stamp}:";
        var offset = 0;
        if (request.Cursor is { } cursor)
        {
            var parts = cursor.Split(':');
            if (parts.Length != 4 || parts[0] != "retention_holds_v2" || parts[1] != identity.ToString() ||
                parts[2].Length != 64 || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out offset))
                return Fail(RequestErrorKind.Validation, "The retention history cursor is invalid.");
            if (parts[2] != retention.Value.Stamp)
                return Fail(RequestErrorKind.Conflict, "The retention history cursor is stale.", true);
        }
        var holds = retention.Value.Holds;
        if (offset > holds.Count)
            return Fail(RequestErrorKind.Validation, "The retention history cursor is invalid.");
        var items = Array.AsReadOnly(holds.Skip(offset).Take(request.Limit ?? 50).ToArray());
        var next = offset + items.Count < holds.Count
            ? prefix + (offset + items.Count).ToString(CultureInfo.InvariantCulture) : null;
        var fence = await ArtifactRetentionSourceReader.CheckAsync(reader, source.Value, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<Page<ArtifactLegalHoldView>>.Failure(fence.Error!);
        var current = await directory.ReadAsync(source.Value, ct).ConfigureAwait(false);
        if (!current.IsSuccess || current.Value.Stamp != retention.Value.Stamp)
            return Fail(RequestErrorKind.Conflict, "Retention changed during this request.", true);
        return Result<Page<ArtifactLegalHoldView>>.Success(new(items, next));
    }

    static Result<Page<ArtifactLegalHoldView>> Fail(RequestErrorKind kind, string message, bool transient = false) =>
        Result<Page<ArtifactLegalHoldView>>.Failure(new RequestError(kind, message, transient));
}
