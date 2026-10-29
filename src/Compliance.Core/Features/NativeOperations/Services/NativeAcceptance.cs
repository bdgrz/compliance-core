using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.NativeOperations.Services;

/// <summary>
/// Owner-side acceptance of one selected logical operation. Reducers hand every native fact to
/// <see cref="Observe"/>; at most one accepted fact may carry the selected logical id, a second one is stream
/// corruption. Decisions compare the full <see cref="AcceptedOperationBinding"/>, never a hash alone.
/// </summary>
public sealed class NativeAcceptance(Uuid selectedLogicalId, string? selectedCommandKind = null)
{
    DomainEvent? _selected;
    OperationProvenance? _provenance;

    /// <summary>The logical id this hydration was selected for; <see cref="Uuid.Empty"/> selects nothing.</summary>
    public Uuid SelectedLogicalId { get; } = selectedLogicalId;

    /// <summary>The accepted fact carrying the selected logical id, once observed.</summary>
    public DomainEvent? SelectedFact => _selected;

    /// <summary>The accepted operation carried by <see cref="SelectedFact"/>.</summary>
    public INativeOperationFact? SelectedOperation => _selected as INativeOperationFact;

    /// <summary>
    /// The command kind this hydration was selected for, or null when the logical id alone selects. Two commands may
    /// share one logical id on one owner when their kinds differ, so the selection is the pair.
    /// </summary>
    public string? SelectedCommandKind { get; } = selectedCommandKind;

    /// <summary>The provenance of <see cref="SelectedFact"/>, including facts that carry it without being native.</summary>
    public OperationProvenance? SelectedProvenance => _provenance;

    /// <summary>Records a native fact when it carries the selected logical id.</summary>
    public void Observe(DomainEvent fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        if (fact is not INativeOperationFact native)
            throw new ArgumentException("Only native operation facts take part in acceptance.", nameof(fact));
        Observe(fact, native.Operation);
    }

    /// <summary>
    /// Records a fact that carries operation provenance without being a native operation fact, such as an
    /// identity fact that is native only when a recovery caused it.
    /// </summary>
    public void Observe(DomainEvent fact, OperationProvenance operation)
    {
        ArgumentNullException.ThrowIfNull(fact);
        ArgumentNullException.ThrowIfNull(operation);
        if (SelectedLogicalId == Uuid.Empty || operation.LogicalId != SelectedLogicalId ||
            SelectedCommandKind is not null && !string.Equals(operation.CommandKind, SelectedCommandKind,
                StringComparison.Ordinal))
            return;
        if (_selected is not null)
            throw new InvalidOperationException(
                $"Logical operation {SelectedLogicalId} is accepted more than once on one owner stream.");
        _selected = fact;
        _provenance = operation;
    }

    /// <summary>Compares a candidate binding with the accepted fact for the selected logical id.</summary>
    public NativeAcceptanceDecision Decide(AcceptedOperationBinding candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.LogicalId != SelectedLogicalId)
            throw new ArgumentException("The candidate binding must name the selected logical id.", nameof(candidate));
        return _provenance is not { } accepted ? NativeAcceptanceDecision.None
            : AcceptedOperationBinding.Of(accepted) == candidate ? NativeAcceptanceDecision.Replayed
            : NativeAcceptanceDecision.Conflict;
    }
}
