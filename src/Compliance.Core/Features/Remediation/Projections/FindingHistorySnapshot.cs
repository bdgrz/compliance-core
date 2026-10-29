using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Remediation.Projections;

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
