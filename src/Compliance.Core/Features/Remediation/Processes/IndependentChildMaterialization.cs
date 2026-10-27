using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
namespace Bdgrz.Compliance.Features.Remediation.Processes;

/// <summary>
///     The unordered process that materializes the independent children admitted by a parent. Every admitted child kind
///     is one root and one receiver of the same single stage; this slice ships the corrective action receiver, and a
///     step whose receiver does not belong to the record's root is not built (the factory returns null), so a child kind
///     that has no reactor yet is never dispatched. A parent closure drains these before its own acquisition stage by
///     waiting on every unmaterialized action.
/// </summary>
sealed class IndependentChildMaterialization : IChildMaterializationReceiver
{
    public const string Materialize = "materialize";

    public string Receiver => nameof(MaterializeCorrectiveAction);

    public async ValueTask<IRequest<ReceiverOutcome>?> CreateAsync(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        // Only the corrective action receiver ships here; a child kind of another owner is dispatched by its own slice.
        if (step.Receiver != nameof(MaterializeCorrectiveAction) || step.Target is null ||
            step.Record.Source.Stream.Area != FindingAggregate.Area)
            return null;
        var record = step.Record;
        var tenant = Uuid.Parse(record.Key.Realm, System.Globalization.CultureInfo.InvariantCulture);
        var content = services.GetRequiredService<IImmutableContentStore>();
        ChildAdmissionIntent intent;
        AddCorrectiveAction authored;
        try
        {
            intent = await FindingContent.ReadAsync<ChildAdmissionIntent>(content, tenant, record.Intent, ct)
                .ConfigureAwait(false);
            authored = await FindingContent.ReadAsync<AddCorrectiveAction>(content, tenant,
                intent.CanonicalAuthoredContent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            throw new SourceFactUnavailableException($"The frozen admission is not available yet: {exception.Message}");
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new SourceFactVerificationException($"The frozen admission cannot be verified: {exception.Message}");
        }

        var target = new CorrectiveAction(tenant, authored.ProgramId, intent.ParentId, intent.ChildId).Stream;
        return new MaterializeCorrectiveAction(step.CommandId, record.Source, target, record.Key.RootOperationId,
            (long)intent.ParentGeneration, null, record.Intent, intent.ParentId, intent.ChildId);
    }
}
