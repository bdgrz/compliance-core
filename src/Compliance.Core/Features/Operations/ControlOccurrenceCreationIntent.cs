using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

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
