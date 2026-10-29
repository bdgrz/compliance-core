using System.Globalization;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;

/// <summary>
///     Builds the <see cref="MaterializeAdvisorFeedback" /> receiver step of <c>IndependentChildMaterialization</c> from
///     the frozen admission of a feedback fact. A step whose record does not come from a handoff is not built.
/// </summary>
sealed class AdvisorFeedbackMaterializationReceiver : IChildMaterializationReceiver
{
    public string Receiver => nameof(MaterializeAdvisorFeedback);

    public async ValueTask<IRequest<ReceiverOutcome>?> CreateAsync(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(services);
        if (step.Target is null || step.Record.Source.Stream.Area != AdvisorHandoff.Area)
            return null;
        var record = step.Record;
        var tenant = Uuid.Parse(record.Key.Realm, CultureInfo.InvariantCulture);
        var content = services.GetRequiredService<IImmutableContentStore>();
        ChildAdmissionIntent intent;
        RecordAdvisorFeedback authored;
        try
        {
            intent = await AdvisorContent.ReadAsync<ChildAdmissionIntent>(content, tenant, record.Intent, ct)
                .ConfigureAwait(false);
            authored = await AdvisorContent.ReadAsync<RecordAdvisorFeedback>(content, tenant,
                intent.CanonicalAuthoredContent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            throw new SourceFactVerificationException($"The frozen admission cannot be verified yet: {exception.Message}");
        }

        var target = new AdvisorFeedbackThread(tenant, authored.ProgramId, intent.ParentId, intent.ChildId).Stream;
        return new MaterializeAdvisorFeedback(step.CommandId, record.Source, target, record.Key.RootOperationId,
            (long)intent.ParentGeneration, null, record.Intent, intent.ParentId, intent.ChildId);
    }
}
