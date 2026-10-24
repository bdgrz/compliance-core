using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     Builds <see cref="MaterializeProviderRiskAcceptanceLink" /> for a process record started by a link admission.
///     Only the record whose source is the admission fact of a gap yields a command.
/// </summary>
sealed class ProviderRiskAcceptanceLinkMaterializer : IChildMaterializationReceiver
{
    public string Receiver => nameof(MaterializeProviderRiskAcceptanceLink);

    public async ValueTask<IRequest<ReceiverOutcome>?> CreateAsync(ProcessStep processStep, IServiceProvider services,
        CancellationToken ct)
    {
        var record = processStep.Record;
        if (processStep.Receiver != Receiver ||
            record.Source.Stream.Area != ProviderAssuranceStreams.Area)
            return null;
        var content = services.GetRequiredService<IImmutableContentStore>();
        var tenantId = Uuid.Parse(record.Key.Realm, System.Globalization.CultureInfo.InvariantCulture);
        ProviderRiskLinkIntent intent;
        try
        {
            intent = await ProviderAssuranceContent.ReadAsync<ProviderRiskLinkIntent>(content, tenantId, record.Intent,
                ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new SourceFactVerificationException($"The frozen link is not readable yet: {exception.Message}");
        }

        return new MaterializeProviderRiskAcceptanceLink(processStep.CommandId, record.Source,
            ProviderAssuranceStreams.RiskAcceptance(tenantId, intent.LinkId), intent.LinkId,
            (long)intent.GapAdmissionRevision, null, record.Intent, intent.GapId, intent.LinkId);
    }
}
