using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Complete immutable native receiver/source history, separate from bounded command authority.</summary>
public static class WeeklyWorkDigestHistory
{
    public static async Task<WeeklyWorkDigestHistorySnapshot> ReadAsync(IDomainEventReader events,
        ISourceFactResolver sources, WeeklyWorkDigest owner, CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var rules = new WeeklyWorkDigestRules(owner.TenantId, owner.Id);
        var digest = WorkDigestReplayDigest.Empty;
        var prefix = new List<DomainEvent>();
        var identities = new HashSet<(Uuid, string)>();
        var verified = new HashSet<(SourceFactProof, int)>();
        foreach (var fact in facts)
        {
            try
            {
                if (fact is IWeeklyWorkDigestOperationFact native)
                {
                    WeeklyDigestNative.Structural(owner, fact, rules.Revision);
                    if (!identities.Add((native.Operation.LogicalId, native.Operation.CommandKind)) || fact.Metadata.Actor != native.OriginalActor)
                        throw WeeklyDigestNative.Invalid("The weekly history repeats an operation or changes its original producing actor.");
                    var view = rules.Read();
                    var before = new WeeklyWorkDigestHeader(view.MemberId, view.WeekStarting, view.ScheduleKey, view.ScheduledAt,
                        view.ScheduledBy, view.Status, view.Revision, view.SuppressionReason);
                    var selectedNumber = fact switch
                    {
                        WeeklyWorkDigestDeliveryRequested requested => requested.Authored.AttemptNumber,
                        WeeklyWorkDigestDeliverySuppressed suppressed => suppressed.Authored.AttemptNumber,
                        WeeklyWorkDigestDeliveryRecorded recorded => recorded.Authored.AttemptNumber,
                        _ => 0
                    };
                    var selected = view.DeliveryAttempts.FirstOrDefault(attempt => attempt.Number == selectedNumber);
                    var latest = view.DeliveryAttempts.Count == 0 ? null : view.DeliveryAttempts[^1];
                    var change = WeeklyDigestNative.ChangeOf(fact, before, selected, latest);
                    if (change.ScheduleSource is { } schedule && verified.Add((schedule, 0)))
                        await WeeklyDigestSources.VerifyAsync(sources, owner, schedule, prefix, 0, ct).ConfigureAwait(false);
                    if (change.RequestSource is { } requestedSource && verified.Add((requestedSource, selectedNumber)))
                        await WeeklyDigestSources.VerifyAsync(sources, owner, requestedSource, prefix, selectedNumber, ct).ConfigureAwait(false);
                    var evaluation = new WeeklyWorkDigestRules(owner.TenantId, owner.Id);
                    evaluation.Restore(before, selected, latest);
                    var evaluated = WeeklyDigestNative.Evaluate(evaluation, change);
                    if (!evaluated.IsSuccess || native.NoChange != (evaluation.PendingEvents.Count == 0))
                        throw WeeklyDigestNative.Invalid("The weekly native receipt changes its exact original business effect.");
                    WeeklyDigestNative.VerifyPrepared(fact, change);
                    if (native.NoChange)
                        rules.AdvanceReceiptRevision(native.Revision);
                    else
                        rules.Replay(fact);
                }
                else
                    rules.Replay(fact);
            }
            catch (SourceFactVerificationException exception)
            {
                if (exception is SourceFactUnavailableException)
                    throw new ImmutableContentUnavailableException("The exact weekly source proof is not visible yet.");
                throw WeeklyDigestNative.Invalid("The weekly source proof is contradictory: " + exception.Message);
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
            {
                throw WeeklyDigestNative.Invalid("The weekly history violates its exact native/source/business transition: " + exception.Message);
            }
            prefix.Add(fact);
            digest = WorkDigestReplayDigest.Append(digest, fact);
        }
        var current = rules.Read();
        var header = current.Revision == 0 ? null : new WeeklyWorkDigestHeader(current.MemberId, current.WeekStarting,
            current.ScheduleKey, current.ScheduledAt, current.ScheduledBy, current.Status, current.Revision, current.SuppressionReason);
        var last = current.DeliveryAttempts.Count == 0 ? null : current.DeliveryAttempts[^1];
        if (header != owner.Header || last != owner.LatestAttempt || owner.SelectedAttemptNumber > 0 &&
            current.DeliveryAttempts.FirstOrDefault(item => item.Number == owner.SelectedAttemptNumber) != owner.SelectedAttempt ||
            facts.Count > 0 && (owner.Latest is not { } actual || !facts[^1].Equals(actual) || facts[^1].Metadata != actual.Metadata))
            throw WeeklyDigestNative.Invalid("The weekly read history differs from its exact hydrated current/selected source cut.");
        owner.ReplayCut.Confirm(owner, digest);
        ct.ThrowIfCancellationRequested();
        return new WeeklyWorkDigestHistorySnapshot(Array.AsReadOnly(facts.ToArray()), current);
    }
}
