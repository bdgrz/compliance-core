using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Commands;
namespace Bdgrz.Compliance.Features.Providers.Processes;

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
