using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Projections;
using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Readers;

sealed class FitzPlatformOperatorDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/platform-operator-directory/projection", "PlatformOperatorDirectory"),
      IPlatformOperatorDirectoryProjection, IPlatformOperatorDirectoryReader
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        var userId = PlatformOperatorDirectoryTransitions.UserId(domainEvent);
        var expectedRevision = PlatformOperatorDirectoryTransitions.ExpectedRevision(domainEvent);
        if (userId == Uuid.Empty || expectedRevision == ulong.MaxValue ||
            domainEvent.Metadata.AggregateId != PlatformOperator.IdentityOf(userId) ||
            domainEvent.Metadata.AggregateVersion != expectedRevision + 1 ||
            domainEvent.Metadata.EventId == Uuid.Empty || domainEvent.Metadata.IsAudit)
            throw new InvalidOperationException("The operator directory fact must come from its exact user owner.");
        var current = await PlatformOperatorDirectorySchema.Directory.GetAsync(Transaction, userId, ct).ConfigureAwait(false);
        var next = PlatformOperatorDirectoryTransitions.Apply(current, domainEvent);
        if (current is null && next is not null)
            await PlatformOperatorDirectorySchema.Directory.InsertAsync(Transaction, next, ct).ConfigureAwait(false);
        else if (current is not null && next is null)
            await PlatformOperatorDirectorySchema.Directory.DeleteAsync(Transaction, current, ct).ConfigureAwait(false);
        else if (current is not null && next is not null)
            await PlatformOperatorDirectorySchema.Directory.ReplaceAsync(Transaction, current, next, ct).ConfigureAwait(false);
    }

    public async ValueTask<Page<PlatformOperatorDirectoryEntry>> ListAsync(int limit, string? cursor,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync("bdgrz", ct).ConfigureAwait(false);
        var page = await PlatformOperatorDirectorySchema.Directory.QueryPrimaryAsync(tx, limit, cursor, ct)
            .ConfigureAwait(false);
        return new Page<PlatformOperatorDirectoryEntry>(page.Items.Select(row =>
            new PlatformOperatorDirectoryEntry(row.UserId, row.GrantEpisodeId, row.PrimaryGeneration > 0)).ToArray(),
            page.NextCursor);
    }
}
