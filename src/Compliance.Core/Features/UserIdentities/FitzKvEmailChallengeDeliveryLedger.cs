using System.Globalization;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Delivery records on <c>kv://bdgrz/email-challenge-deliveries/{shard}</c> (shard = first two hex characters of the
///     challenge id). Key <c>c/{challenge}</c> holds the record. The route lock at BEGIN makes read-compare-write the
///     CAS; a lost lock is retried from BEGIN.
/// </summary>
sealed class FitzKvEmailChallengeDeliveryLedger(IKvClient client) : IEmailChallengeDeliveryLedger
{
    const int Attempts = 4;

    public async Task<EmailChallengeDeliveryRecord?> ReadAsync(Uuid challengeId, CancellationToken ct = default)
    {
        await using var tx = await client.BeginAsync(Route(challengeId), KvMode.ReadOnly, ct).ConfigureAwait(false);
        var found = await tx.GetAsync(RecordKey(challengeId), ct).ConfigureAwait(false);
        return found.Found ? Parse(found.Value!.Value.Span) : null;
    }

    public Task<EmailChallengeDeliveryRecord> OpenAttemptAsync(Uuid challengeId, string purpose, DateTimeOffset now,
        CancellationToken ct = default) =>
        RetryAsync(async () =>
        {
            await using var tx = await client.BeginAsync(Route(challengeId), KvMode.ReadWrite, ct).ConfigureAwait(false);
            var found = await tx.GetAsync(RecordKey(challengeId), ct).ConfigureAwait(false);
            var current = found.Found ? Parse(found.Value!.Value.Span) : null;
            if (current is { IsDelivered: true } or { IsFailed: true } or { HasOpenAttempt: true })
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                return current;
            }

            var next = (current ?? EmailChallengeDeliveryRecord.Empty(challengeId, purpose)).WithNextAttempt(now);
            var stored = next with { Version = (current?.Version ?? 0) + 1 };
            if (current is null)
                await tx.InsertAsync(RecordKey(challengeId), Serialize(stored), ct).ConfigureAwait(false);
            else
                await tx.PutAsync(RecordKey(challengeId), Serialize(stored), ct).ConfigureAwait(false);
            await tx.CommitAsync(KvDurability.Sync, ct).ConfigureAwait(false);
            return stored;
        });

    public async Task<EmailChallengeOutcomeDecision> RecordOutcomeAsync(Uuid challengeId, Uuid attemptId,
        string outcome, string? failureCode, Uuid commandId, DateTimeOffset at, CancellationToken ct = default)
    {
        for (var count = 1; ; count++)
        {
            try
            {
                await using var tx = await client.BeginAsync(Route(challengeId), KvMode.ReadWrite, ct)
                    .ConfigureAwait(false);
                var found = await tx.GetAsync(RecordKey(challengeId), ct).ConfigureAwait(false);
                if (!found.Found)
                {
                    await tx.RollbackAsync(ct).ConfigureAwait(false);
                    return new EmailChallengeOutcomeDecision(EmailChallengeOutcomeKind.UnknownAttempt,
                        EmailChallengeDeliveryRecord.Empty(challengeId, string.Empty));
                }

                var current = Parse(found.Value!.Value.Span);
                var decision = current.Decide(attemptId, outcome, failureCode, commandId, at);
                if (decision.Kind is EmailChallengeOutcomeKind.Closed or EmailChallengeOutcomeKind.Resolved)
                {
                    var stored = decision.Record with { Version = current.Version + 1 };
                    await tx.PutAsync(RecordKey(challengeId), Serialize(stored), ct).ConfigureAwait(false);
                    await tx.CommitAsync(KvDurability.Sync, ct).ConfigureAwait(false);
                    return decision with { Record = stored };
                }

                await tx.RollbackAsync(ct).ConfigureAwait(false);
                return decision;
            }
            catch (KvException exception) when (count < Attempts && FitzKvProcessRecordStore.IsIsolationConflict(exception))
            {
                // Lost the route lock to another writer of this shard; the transaction had no effect.
            }
        }
    }

    static async Task<EmailChallengeDeliveryRecord> RetryAsync(Func<Task<EmailChallengeDeliveryRecord>> attempt)
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

    static string Route(Uuid challengeId) =>
        $"kv://bdgrz/email-challenge-deliveries/{challengeId.ToString("N", CultureInfo.InvariantCulture)[..2]}";

    static byte[] RecordKey(Uuid challengeId) =>
        Encoding.UTF8.GetBytes($"c/{challengeId.ToString("N", CultureInfo.InvariantCulture)}");

    static byte[] Serialize(EmailChallengeDeliveryRecord record) =>
        JsonSerializer.SerializeToUtf8Bytes(record, ComplianceCoreJsonContext.Default.EmailChallengeDeliveryRecord);

    static EmailChallengeDeliveryRecord Parse(ReadOnlySpan<byte> bytes) =>
        JsonSerializer.Deserialize(bytes, ComplianceCoreJsonContext.Default.EmailChallengeDeliveryRecord)
        ?? throw new InvalidOperationException("A delivery record cannot be empty.");
}
