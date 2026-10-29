using System.Globalization;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.AuditorRequests.Processes;

/// <summary>
///     Builds <see cref="MaterializeAuditorSampleSelection" /> for a sample selection admission of
///     <c>IndependentChildMaterialization</c>; its frozen intent is the <see cref="AuditorSampleSelectionIntent" />.
/// </summary>
sealed class AuditorSampleSelectionMaterializationReceiver : IChildMaterializationReceiver
{
    public string Receiver => nameof(MaterializeAuditorSampleSelection);

    public async ValueTask<IRequest<ReceiverOutcome>?> CreateAsync(ProcessStep processStep, IServiceProvider services,
        CancellationToken ct)
    {
        var record = processStep.Record;
        if (processStep.Target is null || record.Source.Stream.Area != AuditorRequest.Area ||
            record.Intent.Schema != AuditorSampleSelectionIntent.Schema)
            return null;
        var tenant = Uuid.Parse(record.Key.Realm, CultureInfo.InvariantCulture);
        AuditorSampleSelectionIntent intent;
        try
        {
            intent = await AuditorContent.ReadAsync<AuditorSampleSelectionIntent>(
                services.GetRequiredService<IImmutableContentStore>(), tenant, record.Intent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            throw new SourceFactVerificationException($"The frozen admission cannot be verified yet: {exception.Message}");
        }

        var target = new AuditorSampleSelection(tenant, intent.ProgramId, intent.RequestId, intent.SelectionId).Stream;
        return new MaterializeAuditorSampleSelection(processStep.CommandId, record.Source, target, record.Key.RootOperationId,
            (long)intent.RequestGeneration, null, record.Intent, intent.RequestId, intent.SelectionId);
    }
}
