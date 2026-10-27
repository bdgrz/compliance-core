using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Processes;
using Bdgrz.Compliance.Features.Readiness.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Readiness.Models;
namespace Bdgrz.Compliance.Features.Readiness.Reactors;

/// <summary>
///     Starts <c>ReadinessDecisionProcess</c> from a committed readiness or Type I entry decision acceptance. The
///     process record, with the complete canonical gap plan key list of the frozen manifest, exists before the source
///     checkpoint advances; the sweep resumes it if this reactor stops. A manifest that cannot be read yet is
///     transient: no record is created from an unverified decision.
/// </summary>
sealed partial class ReadinessDecisionReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process,
    IImmutableContentStore content)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(ProgramReadiness.Area), WorkloadName),
        IReactorHandler<ReadinessDecisionAccepted>, IReactorHandler<TypeIEntryDecisionAccepted>
{
    public const string WorkloadName = "ReadinessDecisionV1";

    public ValueTask HandleAsync(IReactorContext<ReadinessDecisionAccepted> context, CancellationToken ct) =>
        StartAsync(context, context.Trigger.Intent, ct);

    public ValueTask HandleAsync(IReactorContext<TypeIEntryDecisionAccepted> context, CancellationToken ct) =>
        StartAsync(context, context.Trigger.Intent, ct);

    async ValueTask StartAsync(IReactorContext context, Proofs.Models.ImmutableContentRef intentReference, CancellationToken ct)
    {
        var tenant = Uuid.Parse(context.Source.Stream.Realm, System.Globalization.CultureInfo.InvariantCulture);
        IReadOnlyList<string> targets;
        try
        {
            var intent = await ReadinessContent.ReadAsync<ReadinessDecisionIntent>(content, tenant, intentReference, ct)
                .ConfigureAwait(false);
            targets = await ReadinessDecisionProcess.TargetsAsync(content, tenant, intent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The frozen decision cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        _ = await process.StartAsync(ReadinessEffects.DecisionProcessName, context, intentReference, targets,
            $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
    }
}
