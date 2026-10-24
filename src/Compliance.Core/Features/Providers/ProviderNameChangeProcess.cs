using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     The ordered provider name-change process: claim the new then the old name in canonical key order and hold both
///     through the apply, apply the accepted change, then release only the old claim of the matching generation. A
///     definite refusal aborts to <see cref="ProviderNames.CancelReceiver" />, which fences the apply before the
///     newly acquired claim is given back.
/// </summary>
public static class ProviderNameChangeProcess
{
    /// <summary>The registered definition; it equals the catalog entry exactly.</summary>
    public static ProcessDefinition Definition { get; } = new(ProviderNames.ProcessName,
        [nameof(ProviderChangeAccepted)], true,
        [
            new ProcessStageDefinition("claim", [ProviderNames.ClaimReceiver],
                "New then old key in canonical order; hold both through apply."),
            new ProcessStageDefinition("apply", [ProviderNames.ApplyReceiver]),
            new ProcessStageDefinition("release", [ProviderNames.ReleaseReceiver], "Old matching generation only."),
        ],
        [ProviderNames.CancelReceiver],
        CreateReceiverAsync);

    static async ValueTask<IRequest<ReceiverOutcome>?> CreateReceiverAsync(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        var view = await NamedRecordProcessView.OpenAsync(ProviderNames.Descriptor, step, services, ct)
            .ConfigureAwait(false);
        var fields = view.StepFor(view.Root.Owner);
        switch (step.Receiver)
        {
            case ProviderNames.ClaimReceiver:
                {
                    if (view.Target() is not { } claim)
                        return null;
                    var (proof, _, _) = await SourceFactProofs.BuildAsync<ProviderChangeAccepted>(view.Resolver,
                        view.Content, view.Realm, step.Record.Source, ct).ConfigureAwait(false);
                    return new ClaimProviderName(step.CommandId, proof, claim.Stream, fields.OperationId, fields.Generation,
                        fields.ExpectedRevision, fields.Intent, fields.RecordId, claim.NormalizedName, claim.NameKind);
                }
            case ProviderNames.ApplyReceiver:
                {
                    var claimed = view.FactOf(ProviderNames.ClaimReceiver)
                        ?? throw new InvalidOperationException("A provider change always holds its name claim.");
                    var (proof, _, _) = await SourceFactProofs.BuildAsync<ProviderNameClaimed>(view.Resolver, view.Content,
                        view.Realm, claimed, ct).ConfigureAwait(false);
                    return new ApplyProviderChange(step.CommandId, proof, fields.Target, fields.OperationId,
                        fields.Generation, fields.ExpectedRevision, fields.Intent, fields.RecordId);
                }
            case ProviderNames.ReleaseReceiver:
                return await ReleaseAsync(view, step, fields, ct).ConfigureAwait(false);
            case ProviderNames.CancelReceiver:
                {
                    var proof = await view.Content.WriteJsonAsync(view.Realm,
                        Uuid.CreateVersion5(step.CommandId, NameChangeFailureProof.Schema), NameChangeFailureProof.Schema,
                        new NameChangeFailureProof(fields.OperationId, fields.Generation, step.Record.Note ?? "unspecified"),
                        ct).ConfigureAwait(false);
                    return new CancelProviderChange(step.CommandId,
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
        ReleaseProviderNameSourceProof proof;
        if (view.Aborting)
        {
            // Only a canceled change gives its newly acquired claim back, and only with the cancellation as proof.
            if (view.FactOf(ProviderNames.CancelReceiver) is not { } canceled ||
                NamedRecordReceivers.NewOnly(view.Intent) != target.Stream)
                return null;
            proof = new ReleaseProviderNameSourceProof.CanceledProof((await SourceFactProofs
                .BuildAsync<ProviderChangeCanceled>(view.Resolver, view.Content, view.Realm, canceled, ct)
                .ConfigureAwait(false)).Proof);
        }
        else
        {
            if (view.FactOf(ProviderNames.ApplyReceiver) is not { } applied ||
                NamedRecordReceivers.OldOnly(view.Intent) != target.Stream)
                return null;
            proof = view.Intent.ExpectedRecordRevision == 0
                ? new ReleaseProviderNameSourceProof.RecordedProof((await SourceFactProofs
                    .BuildAsync<ProviderRecorded>(view.Resolver, view.Content, view.Realm, applied, ct)
                    .ConfigureAwait(false)).Proof)
                : new ReleaseProviderNameSourceProof.RevisedProof((await SourceFactProofs
                    .BuildAsync<ProviderRevised>(view.Resolver, view.Content, view.Realm, applied, ct)
                    .ConfigureAwait(false)).Proof);
        }

        return new ReleaseProviderName(step.CommandId, proof, target.Stream, fields.OperationId, fields.Generation,
            fields.ExpectedRevision, fields.Intent, fields.RecordId, target.NormalizedName);
    }
}
