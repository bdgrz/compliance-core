using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Every exact immutable weekly transition, including unselected historical attempts, outside command state.</summary>
public static class WeeklyWorkDigestHistory
{
    public static async Task<WeeklyWorkDigestHistorySnapshot> ReadAsync(IDomainEventReader events,
        WeeklyWorkDigest owner, CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var rules = new WeeklyWorkDigestRules(owner.TenantId, owner.Id);
        var digest = WorkDigestReplayDigest.Empty;
        foreach (var fact in facts)
        {
            try
            { rules.Replay(fact); }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
            {
                throw new ImmutableContentIntegrityException("The weekly digest history violates exact scheduling/attempt/outcome rules: " + exception.Message);
            }
            digest = WorkDigestReplayDigest.Append(digest, fact);
        }
        var view = rules.Read();
        var header = view.Revision == 0 ? null : new WeeklyWorkDigestHeader(view.MemberId, view.WeekStarting,
            view.ScheduleKey, view.ScheduledAt, view.ScheduledBy, view.Status, view.Revision, view.SuppressionReason);
        var latest = view.DeliveryAttempts.Count == 0 ? null : view.DeliveryAttempts[^1];
        if (header != owner.Header || latest != owner.LatestAttempt ||
            owner.SelectedAttemptNumber > 0 && view.DeliveryAttempts.FirstOrDefault(item => item.Number == owner.SelectedAttemptNumber) != owner.SelectedAttempt ||
            facts.Count > 0 && facts[^1] is { } tail && (owner.Latest is not { } actual || !tail.Equals(actual) || tail.Metadata != actual.Metadata))
            throw new ImmutableContentIntegrityException("The weekly history differs from exact hydrated current/selected authority.");
        owner.ReplayCut.Confirm(owner, digest);
        ct.ThrowIfCancellationRequested();
        return new WeeklyWorkDigestHistorySnapshot(Array.AsReadOnly(facts.ToArray()), view);
    }
}
