using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Events;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Complete technical dispatch history at the exact hydrated native and physical source cut.</summary>
public static class WorkDigestDispatchHistory
{
    public static async Task<WorkDigestDispatchHistorySnapshot> ReadAsync(IDomainEventReader events,
        WorkDigestDispatch owner, CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var rules = new WorkDigestDispatchRules(owner.TenantId, owner.Id);
        var digest = WorkDigestReplayDigest.Empty;
        ulong revision = 0;
        var authorizations = new HashSet<Uuid>();
        foreach (var fact in facts)
        {
            var (tenant, member, week) = Scope(fact);
            if (tenant != owner.TenantId || member != owner.Id)
                throw Invalid("The dispatch history belongs to another tenant or member.");
            var state = rules.States.GetValueOrDefault(week);
            switch (fact)
            {
                case WorkDigestDispatchScheduled scheduled:
                    if (state is not null || rules.States.Keys.Any(existing => existing > week) ||
                        WorkDigestSchedule.NormalizeTimeZoneId(scheduled.TimeZoneId) != scheduled.TimeZoneId)
                        throw Invalid("The dispatch history repeats or reverses an immutable weekly schedule.");
                    break;
                case WorkDigestDispatchAttemptStarted started:
                    if (state is null || started.Attempt != state.Attempts + 1 || started.StartedAt < state.ScheduledAt ||
                        started.StartedAt >= state.RetryDeadline || !WorkDigestSchedule.IsCurrentWeek(week, state.TimeZoneId, started.StartedAt) ||
                        state.Status != WorkDigestDispatch.Scheduled && (state.Status != WorkDigestDispatch.RetryPending ||
                            state.NextAttemptAt is not { } next || started.StartedAt < next))
                        throw Invalid("The dispatch attempt differs from its exact scheduled/retry authority.");
                    break;
                case WorkDigestDispatchOutcomeRecorded outcome:
                    if (state is null || !AllowedOutcome(state.Status, outcome.Status) ||
                        (outcome.Status == WorkDigestDispatch.RetryPending) != (outcome.NextAttemptAt is not null))
                        throw Invalid("The dispatch outcome has no matching exact pending/in-flight attempt.");
                    break;
                case WorkDigestUnknownRetryAuthorized authorization:
                    WorkDigestRetryContent.Validate(authorization, owner, revision, state);
                    if (authorization.Metadata.Actor != authorization.OriginalActor)
                        throw Invalid("The native retry's producing actor differs from its original operator.");
                    if (!authorizations.Add(authorization.Operation.LogicalId))
                        throw Invalid("The member dispatch repeats a native authorization command identity.");
                    if (state is null || state.Status != WorkDigestDispatch.Unknown || state.MessageId != authorization.MessageId ||
                        state.Attempts != authorization.PriorAttempt || authorization.AuthorizedBy == Uuid.Empty ||
                        authorization.AuthorizedAt < state.ScheduledAt || authorization.AuthorizedAt >= state.RetryDeadline ||
                        !WorkDigestSchedule.IsCurrentWeek(week, state.TimeZoneId, authorization.AuthorizedAt) ||
                        !AuditValue(authorization.EvidenceReference, 256) || !AuditValue(authorization.Rationale, 512))
                        throw Invalid("The retry authorization differs from its exact unknown message/attempt/window and evidence.");
                    break;
                default:
                    throw Invalid("The dispatch history contains another fact family.");
            }
            rules.Replay(fact);
            revision++;
            digest = WorkDigestReplayDigest.Append(digest, fact);
        }
        var latest = rules.States.Values.OrderByDescending(item => item.WeekOf).FirstOrDefault();
        var selected = rules.States.GetValueOrDefault(owner.SelectedWeek);
        if (latest != owner.LatestState || owner.SelectedWeek != default && selected != owner.SelectedState ||
            facts.Count > 0 && facts[^1] is { } tail && (owner.Latest is not { } actual || !tail.Equals(actual) || tail.Metadata != actual.Metadata))
            throw Invalid("The dispatch read history differs from its exact hydrated current/selected authority.");
        owner.ReplayCut.Confirm(owner, digest);
        ct.ThrowIfCancellationRequested();
        return new WorkDigestDispatchHistorySnapshot(facts, rules);
    }
    static bool AllowedOutcome(string before, string after) => after switch
    {
        WorkDigestDispatch.Sent or WorkDigestDispatch.PermanentFailure or WorkDigestDispatch.Unknown or
            WorkDigestDispatch.RetryPending => before == WorkDigestDispatch.InFlight,
        WorkDigestDispatch.Skipped => before is WorkDigestDispatch.Scheduled or WorkDigestDispatch.RetryPending,
        WorkDigestDispatch.RetryExhausted => before is WorkDigestDispatch.InFlight or WorkDigestDispatch.Scheduled or WorkDigestDispatch.RetryPending,
        _ => false
    };
    static bool AuditValue(string value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum &&
        value == value.Trim() && !value.Any(char.IsControl);
    internal static (Uuid Tenant, Uuid Member, DateOnly Week) Scope(DomainEvent fact) => fact switch
    {
        WorkDigestDispatchScheduled source => (source.TenantId, source.MemberId, source.WeekOf),
        WorkDigestDispatchAttemptStarted source => (source.TenantId, source.MemberId, source.WeekOf),
        WorkDigestDispatchOutcomeRecorded source => (source.TenantId, source.MemberId, source.WeekOf),
        WorkDigestUnknownRetryAuthorized source => (source.TenantId, source.MemberId, source.WeekOf),
        _ => throw Invalid("The dispatch history contains another native fact kind.")
    };
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
