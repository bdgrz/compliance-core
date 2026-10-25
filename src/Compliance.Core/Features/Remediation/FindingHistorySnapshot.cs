using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Explicit verified read composition; these historical collections are never retained by command authority.</summary>
public sealed class FindingHistorySnapshot
{
    internal FindingHistorySnapshot(IReadOnlyList<DomainEvent> facts) => Facts = Array.AsReadOnly(facts.ToArray());

    /// <summary>All native finding facts at the verified exact physical cut.</summary>
    public IReadOnlyList<DomainEvent> Facts { get; }
    /// <summary>Every admitted child in original admission order.</summary>
    public IReadOnlyList<CorrectiveActionAdmitted> Admitted => Facts.OfType<CorrectiveActionAdmitted>().ToArray();
    /// <summary>Every recorded acceptance link.</summary>
    public IReadOnlyList<FindingAcceptanceLinkedV2> Acceptances => Facts.OfType<FindingAcceptanceLinkedV2>().ToArray();
    /// <summary>Every closed generation, including superseded closures.</summary>
    public IReadOnlyList<FindingClosedV2> ClosureHistory => Facts.OfType<FindingClosedV2>().ToArray();
    /// <summary>The exact immutable source fact.</summary>
    public DomainEvent? FactById(Uuid eventId) => Facts.FirstOrDefault(fact => fact.Metadata.EventId == eventId);
    /// <summary>The exact historical closure admission.</summary>
    public FindingClosureAccepted? AcceptedClosure(Uuid rootOperationId) => Facts.OfType<FindingClosureAccepted>()
        .FirstOrDefault(fact => fact.Operation.RootOperationId == rootOperationId);
    /// <summary>The exact historical closure decision or cancellation.</summary>
    public DomainEvent? TerminalClosure(Uuid rootOperationId) => Facts.FirstOrDefault(fact => fact switch
    {
        FindingClosedV2 closed => closed.Operation.RootOperationId == rootOperationId,
        FindingClosureCanceled canceled => canceled.Operation.RootOperationId == rootOperationId,
        _ => false,
    });
}
