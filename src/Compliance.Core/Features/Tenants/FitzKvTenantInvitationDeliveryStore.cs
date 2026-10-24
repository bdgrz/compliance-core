using System.Globalization;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Invitation delivery records on <c>kv://bdgrz/tenant-invitation-deliveries/{shard}</c> (shard = first two hex
///     characters of the invitation id), keyed <c>{tenant}/d/{invitation}</c>. The route lock at BEGIN makes
///     read-compare-write the CAS; a lost lock is retried from BEGIN.
/// </summary>
sealed class FitzKvTenantInvitationDeliveryStore(IKvClient client) : ITenantInvitationDeliveryStore
{
    internal const int Attempts = 4;

    public async Task<TenantInvitationDeliveryRecord?> ReadAsync(Uuid tenantId, Uuid invitationId,
        CancellationToken ct = default)
    {
        await using var tx = await client.BeginAsync(Route(invitationId), KvMode.ReadOnly, ct).ConfigureAwait(false);
        var found = await tx.GetAsync(RecordKey(tenantId, invitationId), ct).ConfigureAwait(false);
        return found.Found ? Parse(found.Value!.Value.Span) : null;
    }

    public Task<TenantInvitationDeliveryRecord> CreateIfAbsentAsync(TenantInvitationDeliveryRecord record,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        return RetryAsync(async () =>
        {
            await using var tx = await client.BeginAsync(Route(record.InvitationId), KvMode.ReadWrite, ct)
                .ConfigureAwait(false);
            var key = RecordKey(record.TenantId, record.InvitationId);
            var found = await tx.GetAsync(key, ct).ConfigureAwait(false);
            if (found.Found)
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                return Parse(found.Value!.Value.Span);
            }

            var stored = record with { Version = 1 };
            await tx.InsertAsync(key, Serialize(stored), ct).ConfigureAwait(false);
            await tx.CommitAsync(KvDurability.Sync, ct).ConfigureAwait(false);
            return stored;
        });
    }

    public Task<TenantInvitationDeliveryRecord> ReplaceAsync(TenantInvitationDeliveryRecord record,
        long expectedVersion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        return RetryAsync(async () =>
        {
            await using var tx = await client.BeginAsync(Route(record.InvitationId), KvMode.ReadWrite, ct)
                .ConfigureAwait(false);
            var key = RecordKey(record.TenantId, record.InvitationId);
            var found = await tx.GetAsync(key, ct).ConfigureAwait(false);
            if (!found.Found)
                throw new ProcessConcurrencyException($"Delivery of invitation {record.InvitationId} no longer exists.");
            var current = Parse(found.Value!.Value.Span);
            if (current.Version != expectedVersion)
                throw new ProcessConcurrencyException(
                    $"Delivery of invitation {record.InvitationId} is at version {current.Version}, not {expectedVersion}.");
            var stored = record with { Version = expectedVersion + 1 };
            await tx.PutAsync(key, Serialize(stored), ct).ConfigureAwait(false);
            await tx.CommitAsync(KvDurability.Sync, ct).ConfigureAwait(false);
            return stored;
        });
    }

    static async Task<TenantInvitationDeliveryRecord> RetryAsync(Func<Task<TenantInvitationDeliveryRecord>> attempt)
    {
        for (var count = 1; ; count++)
        {
            try
            {
                return await attempt().ConfigureAwait(false);
            }
            catch (KvException exception) when (count < Attempts && FitzKvProcessRecordStore.IsIsolationConflict(exception))
            {
                // Lost the route lock to another writer of this shard; the transaction had no effect.
            }
        }
    }

    static string Route(Uuid invitationId) =>
        $"kv://bdgrz/tenant-invitation-deliveries/{invitationId.ToString("N", CultureInfo.InvariantCulture)[..2]}";

    static byte[] RecordKey(Uuid tenantId, Uuid invitationId) =>
        Encoding.UTF8.GetBytes($"{tenantId}/d/{invitationId.ToString("N", CultureInfo.InvariantCulture)}");

    static byte[] Serialize(TenantInvitationDeliveryRecord record) =>
        JsonSerializer.SerializeToUtf8Bytes(record, ComplianceCoreJsonContext.Default.TenantInvitationDeliveryRecord);

    static TenantInvitationDeliveryRecord Parse(ReadOnlySpan<byte> bytes) =>
        JsonSerializer.Deserialize(bytes, ComplianceCoreJsonContext.Default.TenantInvitationDeliveryRecord)
        ?? throw new InvalidOperationException("A delivery record cannot be empty.");
}
