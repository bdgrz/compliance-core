using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Builds <see cref="MaterializeAuditorComment" /> for a comment admission of <c>IndependentChildMaterialization</c>.</summary>
sealed class AuditorCommentMaterializationReceiver : IChildMaterializationReceiver
{
    public string Receiver => nameof(MaterializeAuditorComment);

    public async ValueTask<IRequest<ReceiverOutcome>?> CreateAsync(ProcessStep processStep, IServiceProvider services,
        CancellationToken ct)
    {
        if (await AuditorChildMaterializationReceivers.AdmissionAsync(processStep, AdmittedChildKind.AuditorComment, services, ct)
                .ConfigureAwait(false) is not { } intent)
            return null;
        var record = processStep.Record;
        var authored = await AuditorChildMaterializationReceivers.AuthoredAsync<AuditorCommentContent>(processStep, intent,
            services, ct).ConfigureAwait(false);
        var tenant = Uuid.Parse(record.Key.Realm, CultureInfo.InvariantCulture);
        var target = new AuditorFeedbackThread(tenant, authored.ProgramId, intent.ParentId, intent.ChildId).Stream;
        return new MaterializeAuditorComment(processStep.CommandId, record.Source, target, record.Key.RootOperationId,
            (long)intent.ParentGeneration, null, record.Intent, intent.ParentId, intent.ChildId);
    }
}
