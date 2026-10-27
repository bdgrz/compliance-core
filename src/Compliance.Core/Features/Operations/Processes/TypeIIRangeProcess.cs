using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Operations.Reactors;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.Operations.Processes;

/// <summary>
///     The ordered Type II range process: acquire the exact old and new range on the program calendar, apply the
///     persisted opening or amendment to the period with that permit, then settle the calendar. The root is the
///     opening or amendment acceptance, so the complete intent exists on the period before the calendar is touched.
///     A cancellation or rejection recorded on the period fences a delayed apply; the matching release is dispatched
///     by <see cref="TypeIIRangeReleaseReactor" /> from that fence, never from missing state. The abort receivers are
///     dispatched by the receivers that prove them (the rejection) or by the caller (the cancellation), so the
///     factory builds no command for them.
/// </summary>
static class TypeIIRangeProcess
{
    public const string Acquire = "acquire";
    public const string Apply = "apply";
    public const string Settle = "settle";

    public static ProcessDefinition Definition() => new(TypeIIRangeEffects.ProcessName,
        [nameof(TypeIIPeriodOpeningAccepted), nameof(TypeIIPeriodAmendmentAccepted)], true,
        [
            new ProcessStageDefinition(Acquire, [nameof(AcquireTypeIIPeriodRange)],
                "Old and new range held until applied."),
            new ProcessStageDefinition(Apply, [nameof(ApplyTypeIIPeriodRange)]),
            new ProcessStageDefinition(Settle, [nameof(SettleTypeIIPeriodRange)]),
        ], [nameof(RejectTypeIIPeriodOpening), nameof(RejectTypeIIPeriodAmendment),
            nameof(CancelTypeIIPeriodAmendment)], Receiver);

    static async ValueTask<IRequest<ReceiverOutcome>?> Receiver(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        var record = step.Record;
        var realm = record.Key.Realm;
        var intent = await TypeIIRangeContent.ReadIntentAsync(services.GetRequiredService<IImmutableContentStore>(),
            realm, record.Intent, ct).ConfigureAwait(false);
        var tenant = Uuid.Parse(realm, System.Globalization.CultureInfo.InvariantCulture);
        var root = record.Key.RootOperationId;
        var calendar = new TypeIIObservationCalendar(tenant, intent.ProgramId).Stream;
        var period = new TypeIIPeriod(tenant, intent.ProgramId, intent.PeriodId).Stream;
        return step.Receiver switch
        {
            nameof(AcquireTypeIIPeriodRange) => new AcquireTypeIIPeriodRange(step.CommandId, record.Source, calendar,
                root, TypeIIRangeEffects.Generation, null, record.Intent, intent.PeriodId, intent.OldRange,
                intent.NewRange),
            nameof(ApplyTypeIIPeriodRange) => new ApplyTypeIIPeriodRange(step.CommandId,
                Produced(record, nameof(AcquireTypeIIPeriodRange)), period, root, TypeIIRangeEffects.Generation,
                null, record.Intent, intent.PeriodId),
            nameof(SettleTypeIIPeriodRange) => SettleOf(step, calendar, root, intent),
            nameof(RejectTypeIIPeriodOpening) or nameof(RejectTypeIIPeriodAmendment) or
                nameof(CancelTypeIIPeriodAmendment) => null,
            _ => throw new InvalidOperationException($"{step.Receiver} is not a Type II range receiver."),
        };
    }

    static SettleTypeIIPeriodRange? SettleOf(ProcessStep step, EventStreamAddress calendar, Uuid root,
        TypeIIRangeIntent intent)
    {
        // The period fact that ended the operation proves the settlement: the applied fact on the happy path, the
        // rejection when the acquisition was refused. With neither, nothing may be settled.
        var record = step.Record;
        var source = record.Facts.GetValueOrDefault(nameof(ApplyTypeIIPeriodRange))
            ?? record.Facts.GetValueOrDefault(nameof(RejectTypeIIPeriodOpening))
            ?? record.Facts.GetValueOrDefault(nameof(RejectTypeIIPeriodAmendment));
        return source is null
            ? null
            : new SettleTypeIIPeriodRange(step.CommandId, source, calendar, root, TypeIIRangeEffects.Generation, null,
                record.Intent, intent.PeriodId);
    }

    static NativeFactReference Produced(ProcessRecord record, string receiver) =>
        record.Facts.GetValueOrDefault(receiver)
        ?? throw new SourceFactVerificationException($"The {receiver} fact is not recorded on the process yet.");
}
