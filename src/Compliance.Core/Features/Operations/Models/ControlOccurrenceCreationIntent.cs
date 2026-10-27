using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>
///     The complete frozen terms of one occurrence creation. Every generator freezes exactly these values before it
///     selects a window; <paramref name="OccurrenceId" /> derives solely from the tenant, program, control and
///     <paramref name="Window" />. <paramref name="PlanProof" /> is the plan approval fact that put
///     <paramref name="PlanVersionId" /> into effect. Two intents for one window that differ in plan, holders,
///     reviewer or trigger conflict; generator, period and selection time never do.
/// </summary>
public sealed record ControlOccurrenceCreationIntent(DurableIntentHeader Header, Uuid TenantId, Uuid ProgramId,
    Uuid ControlId, Uuid OccurrenceId, ControlBusinessWindow Window, Uuid PlanVersionId,
    NativeFactReference PlanProof, OperatingHolder Owner, OperatingHolder? BackupOwner, Uuid ReviewerMemberId,
    string Trigger, DateTimeOffset SelectedAt)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "control-occurrence-creation-intent.v1";

    /// <summary>The trigger every recurring period selection freezes.</summary>
    public const string RecurringTrigger = "recurring_schedule";

    /// <summary>True when both intents create the same occurrence: same window, plan version, holders, reviewer and trigger.</summary>
    public bool SameTermsAs(ControlOccurrenceCreationIntent other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return TenantId == other.TenantId && ProgramId == other.ProgramId && ControlId == other.ControlId &&
               OccurrenceId == other.OccurrenceId && Window == other.Window && PlanVersionId == other.PlanVersionId &&
               Owner == other.Owner && BackupOwner == other.BackupOwner &&
               ReviewerMemberId == other.ReviewerMemberId && string.Equals(Trigger, other.Trigger,
                   StringComparison.Ordinal);
    }
}
