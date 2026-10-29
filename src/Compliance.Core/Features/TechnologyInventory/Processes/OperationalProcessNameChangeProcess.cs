using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
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
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Commands;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Processes;

/// <summary>
///     The ordered operational process name-change process: claim the names the change needs in canonical key order and hold them
///     through the apply, apply the accepted change, then release only the old claim of the matching generation. A
///     definite refusal aborts to <see cref="OperationalProcessNames.CancelReceiver" />, which fences the apply before the newly
///     acquired claim is given back. A change that claims nothing, such as editing a retired operational process, applies straight
///     from the accepted fact.
/// </summary>
public static class OperationalProcessNameChangeProcess
{
    /// <summary>The registered definition; it equals the catalog entry exactly.</summary>
    public static ProcessDefinition Definition { get; } = new(OperationalProcessNames.ProcessName,
        [nameof(OperationalProcessChangeAccepted)], true,
        [
            new ProcessStageDefinition("claim", [OperationalProcessNames.ClaimReceiver]),
            new ProcessStageDefinition("apply", [OperationalProcessNames.ApplyReceiver]),
            new ProcessStageDefinition("release", [OperationalProcessNames.ReleaseReceiver]),
        ],
        [OperationalProcessNames.CancelReceiver],
        CreateReceiverAsync);

    static async ValueTask<IRequest<ReceiverOutcome>?> CreateReceiverAsync(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        var view = await NamedRecordProcessView.OpenAsync(OperationalProcessNames.Descriptor, step, services, ct)
            .ConfigureAwait(false);
        var fields = view.StepFor(view.Root.Owner);
        switch (step.Receiver)
        {
            case OperationalProcessNames.ClaimReceiver:
                {
                    if (view.Target() is not { } claim)
                        return null;
                    var (proof, _, _) = await SourceFactProofs.BuildAsync<OperationalProcessChangeAccepted>(view.Resolver,
                        view.Content, view.Realm, step.Record.Source, ct).ConfigureAwait(false);
                    return new ClaimOperationalProcessName(step.CommandId, proof, claim.Stream, fields.OperationId, fields.Generation,
                        fields.ExpectedRevision, fields.Intent, fields.RecordId, claim.NormalizedName, claim.NameKind);
                }
            case OperationalProcessNames.ApplyReceiver:
                {
                    ApplyOperationalProcessChangeSourceProof source;
                    if (view.NewClaimFact(OperationalProcessNames.ClaimReceiver) is { } claimed)
                        source = new ApplyOperationalProcessChangeSourceProof.ClaimedProof((await SourceFactProofs
                            .BuildAsync<OperationalProcessNameClaimed>(view.Resolver, view.Content, view.Realm, claimed, ct)
                            .ConfigureAwait(false)).Proof);
                    else
                        source = new ApplyOperationalProcessChangeSourceProof.AcceptedProof((await SourceFactProofs
                            .BuildAsync<OperationalProcessChangeAccepted>(view.Resolver, view.Content, view.Realm, step.Record.Source, ct)
                            .ConfigureAwait(false)).Proof);
                    return new ApplyOperationalProcessChange(step.CommandId, source, fields.Target, fields.OperationId,
                        fields.Generation, fields.ExpectedRevision, fields.Intent, fields.RecordId);
                }
            case OperationalProcessNames.ReleaseReceiver:
                return await ReleaseAsync(view, step, fields, ct).ConfigureAwait(false);
            case OperationalProcessNames.CancelReceiver:
                {
                    var proof = await view.Content.WriteJsonAsync(view.Realm,
                        Uuid.CreateVersion5(step.CommandId, NameChangeFailureProof.Schema), NameChangeFailureProof.Schema,
                        new NameChangeFailureProof(fields.OperationId, fields.Generation, step.Record.Note ?? "unspecified"),
                        ct).ConfigureAwait(false);
                    return new CancelOperationalProcessChange(step.CommandId,
                        Uuid.Parse(view.Realm, System.Globalization.CultureInfo.InvariantCulture), fields.RecordId,
                        fields.OperationId, fields.ExpectedRevision ?? 0, proof);
                }
            default:
                return null;
        }
    }

    static async Task<IRequest<ReceiverOutcome>?> ReleaseAsync(NamedRecordProcessView view, ProcessStep step,
        NameChangeStep fields, CancellationToken ct)
    {
        if (view.Target() is not { } target)
            return null;
        ReleaseOperationalProcessNameSourceProof proof;
        if (view.Aborting)
        {
            // Only a canceled change gives its newly acquired claim back, and only with the cancellation as proof.
            if (view.FactOf(OperationalProcessNames.CancelReceiver) is not { } canceled ||
                NamedRecordReceivers.NewOnly(view.Intent) != target.Stream)
                return null;
            proof = new ReleaseOperationalProcessNameSourceProof.CanceledProof((await SourceFactProofs
                .BuildAsync<OperationalProcessChangeCanceled>(view.Resolver, view.Content, view.Realm, canceled, ct)
                .ConfigureAwait(false)).Proof);
        }
        else
        {
            if (view.FactOf(OperationalProcessNames.ApplyReceiver) is not { } applied ||
                NamedRecordReceivers.OldOnly(view.Intent) != target.Stream)
                return null;
            proof = new ReleaseOperationalProcessNameSourceProof.AppliedProof((await SourceFactProofs
                .BuildAsync<OperationalProcessRevisionRecorded>(view.Resolver, view.Content, view.Realm, applied, ct)
                .ConfigureAwait(false)).Proof);
        }

        return new ReleaseOperationalProcessName(step.CommandId, proof, target.Stream, fields.OperationId, fields.Generation,
            fields.ExpectedRevision, fields.Intent, fields.RecordId, target.NormalizedName);
    }
}
