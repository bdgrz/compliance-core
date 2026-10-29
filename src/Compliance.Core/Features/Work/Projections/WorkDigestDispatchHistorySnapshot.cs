using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Rules;
using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.Work.Projections;

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
