using System.Globalization;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Builds <see cref="MaterializeAuditorResponse" /> for a response admission of <c>IndependentChildMaterialization</c>.</summary>
sealed class AuditorResponseMaterializationReceiver : IChildMaterializationReceiver
{
    public string Receiver => nameof(MaterializeAuditorResponse);

    public async ValueTask<IRequest<ReceiverOutcome>?> CreateAsync(ProcessStep processStep, IServiceProvider services,
        CancellationToken ct)
    {
        if (await AuditorChildMaterializationReceivers.AdmissionAsync(processStep, AdmittedChildKind.AuditorResponse, services, ct)
                .ConfigureAwait(false) is not { } intent)
            return null;
        var record = processStep.Record;
        var authored = await AuditorChildMaterializationReceivers.AuthoredAsync<AuditorResponseDraftContent>(processStep,
            intent, services, ct).ConfigureAwait(false);
        var tenant = Uuid.Parse(record.Key.Realm, CultureInfo.InvariantCulture);
        var target = new AuditorResponse(tenant, authored.ProgramId, intent.ParentId, intent.ChildId).Stream;
        return new MaterializeAuditorResponse(processStep.CommandId, record.Source, target, record.Key.RootOperationId,
            (long)intent.ParentGeneration, null, record.Intent, intent.ParentId, intent.ChildId);
    }
}
