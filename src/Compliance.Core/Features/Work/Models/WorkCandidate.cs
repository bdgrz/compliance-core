using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Projections;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Work.Models;

/// <summary>
///     One open unit of source work before accountability and visibility are applied.
///     <c>Excluded</c> lists members the source workflow's separation of duties rejects.
/// </summary>
public sealed record WorkCandidate(Uuid WorkItemId, string Kind, Uuid SourceId, Uuid? ControlId,
    Uuid? FindingId, string Summary, string Reason, DateOnly? DueOn, string? Materiality,
    string NextAction, string ActionPath, OperatingHolder Responsible, OperatingHolder? Backup,
    IReadOnlySet<Uuid> Excluded, DateTimeOffset CreatedAt)
{
    /// <summary>The owning Program of a source candidate, when the source carries one.</summary>
    public Uuid? ProgramId { get; init; }

    /// <summary>Exact immutable campaign scope for access-review source visibility, including after completion.</summary>
    public Uuid? AccessReviewCampaignId { get; init; }

    /// <summary>Current source system whose visibility must be checked for each actor read.</summary>
    public Uuid? RestrictedSystemInstanceId { get; init; }

    /// <summary>Whether the current reviewer needs an exact-scope source waiver to decide.</summary>
    public bool RequiresSeparationOfDutiesWaiver { get; init; }

    /// <summary>Source-management permission required in addition to the named operating duty.</summary>
    public Uuid? RequiredManagementProgramId { get; init; }

    /// <summary>Runtime source capture for conditional policy proxy recording; never persisted as authority.</summary>
    public PolicyAcknowledgementPersonSnapshot? AcknowledgementPerson { get; init; }

    public static Uuid IdFor(Uuid sourceId, string kind) => Uuid.CreateVersion5(sourceId,
        "work:" + kind);
}
