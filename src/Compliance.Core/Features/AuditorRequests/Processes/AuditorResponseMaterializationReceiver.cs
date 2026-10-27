using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.AuditorRequests.Processes;

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
