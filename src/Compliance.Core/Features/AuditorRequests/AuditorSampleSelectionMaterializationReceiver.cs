using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Features.AuditorRequests;

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
