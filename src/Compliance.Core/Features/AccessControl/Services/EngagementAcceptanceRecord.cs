using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>
///     The facts of one acceptance or staff assignment on its engagement owner, in the order they were appended. The
///     operation pins the engagement content from its root admission until it records or rejects.
/// </summary>
public sealed class EngagementAcceptanceRecord(DomainEvent requested, EngagementIndependenceOperationKind kind)
{
    /// <summary>The root admission, which freezes the operation intent and pins the engagement content.</summary>
    public DomainEvent Requested { get; } = requested;

    /// <summary>Whether this is an acceptance or a positive staff assignment.</summary>
    public EngagementIndependenceOperationKind Kind { get; } = kind;

    /// <summary>The frozen intent of the operation.</summary>
    public ImmutableContentRef Intent { get; init; } = default!;

    /// <summary>The irreversible authorization, once appended.</summary>
    public EngagementPracticeCommitmentsAuthorized? Authorized { get; internal set; }

    /// <summary>The pre-authorization rejection, once appended.</summary>
    public DomainEvent? Rejected { get; internal set; }

    /// <summary>The recorded acceptance or assignment, once appended.</summary>
    public DomainEvent? Completed { get; internal set; }

    /// <summary>The terminal fact: the completion, or the rejection.</summary>
    public DomainEvent? Terminal => Completed ?? Rejected;

    /// <summary>True while the operation still pins the engagement content.</summary>
    public bool IsPending => Terminal is null;

    /// <summary>The root admission as a native operation fact.</summary>
    public INativeOperationFact RequestedOperation => (INativeOperationFact)Requested;
}
