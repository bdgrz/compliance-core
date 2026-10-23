using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

sealed class FitzPlatformUserDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/platform-user-directory-v2/projection",
        "PlatformUserDirectoryV2"),
      IPlatformUserDirectoryProjection, IPlatformUserDirectoryReader, IUserDisplayNameReader
{
    const string Realm = "bdgrz";

    public async ValueTask<string?> ReadAsync(Uuid userId, CancellationToken ct = default)
    {
        if (userId == Uuid.Empty)
            return null;
        await using var tx = await BeginReadAsync(Realm, ct).ConfigureAwait(false);
        return (await PlatformUserDirectorySchema.Directory.GetAsync(tx, userId, ct)
            .ConfigureAwait(false))?.DisplayName;
    }

    public async ValueTask ApplyAsync(UserIdentityProfileObserved observed, CancellationToken ct = default)
    {
        var current = await PlatformUserDirectorySchema.Directory.GetAsync(Transaction,
            observed.UserId, ct).ConfigureAwait(false);
        if (current is null)
            throw new InvalidOperationException("An identity profile cannot project before registration.");
        // Audit sessions can commit out of admission order. A delayed older observation must
        // not overwrite a newer provider update or name removal, including during rebuild.
        if (current.ProfileObservedAt is { } prior && (observed.ObservedAt < prior ||
            observed.ObservedAt == prior && current.ProfileObservationId is { } priorId &&
            StringComparer.Ordinal.Compare(observed.Metadata.EventId.ToString(), priorId.ToString()) <= 0))
            return;
        await PlatformUserDirectorySchema.Directory.ReplaceAsync(Transaction, current,
            current with
            {
                DisplayName = observed.DisplayName,
                ProfileObservedAt = observed.ObservedAt,
                ProfileObservationId = observed.Metadata.EventId,
            }, ct).ConfigureAwait(false);
    }

    public async ValueTask ApplyAsync(UserIdentityRegistered registered,
        CancellationToken ct = default)
    {
        if (await PlatformUserDirectorySchema.Directory.GetAsync(Transaction,
                registered.UserId, ct).ConfigureAwait(false) is null)
            await PlatformUserDirectorySchema.Directory.InsertAsync(Transaction,
                new PlatformUserDirectoryEntry(registered.UserId), ct).ConfigureAwait(false);
    }

    public async ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default)
    {
        if (userId == Uuid.Empty)
            return false;
        await using var tx = await BeginReadAsync(Realm, ct).ConfigureAwait(false);
        return await PlatformUserDirectorySchema.Directory.GetAsync(tx, userId, ct)
            .ConfigureAwait(false) is not null;
    }
}
