using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Explicit complete dispatch read composition; collections never belong to Aggregate command authority.</summary>
public sealed class WorkDigestDispatchHistorySnapshot
{
    readonly WorkDigestDispatchRules _rules;
    internal WorkDigestDispatchHistorySnapshot(IReadOnlyList<DomainEvent> facts, WorkDigestDispatchRules rules)
    {
        Facts = Array.AsReadOnly(facts.ToArray());
        _rules = rules;
    }
    public IReadOnlyList<DomainEvent> Facts { get; }
    public long Revision => Facts.Count;
    public WorkDigestDispatchStatusView Read(DateOnly week) => _rules.Read(week) with { Revision = Revision };
    public WorkDigestDispatchStatusView? FindPending() => _rules.FindPending() is { } pending ? pending with { Revision = Revision } : null;
    public WorkDigestDispatchStatusView? FindCurrentWindow(DateTimeOffset now) => _rules.FindCurrentWindow(now) is { } current ? current with { Revision = Revision } : null;
    public DateTimeOffset RetryDeadline(DateOnly week) => _rules.RetryDeadline(week);
}
