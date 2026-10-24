using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Starts <c>AuditorRequestResolutionProcess</c> from a committed resolution acceptance. The process record, with the
///     complete canonical response key list of the frozen manifest, exists before the source checkpoint advances; the
///     sweep resumes it if this reactor stops. A manifest that cannot be read yet is transient: no record is created from
///     an unverified resolution.
/// </summary>
sealed partial class AuditorRequestResolutionReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process,
    IImmutableContentStore content)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(AuditorRequest.Area), WorkloadName),
        IReactorHandler<AuditorRequestResolutionAccepted>
{
    public const string WorkloadName = "AuditorRequestResolutionV1";

    public async ValueTask HandleAsync(IReactorContext<AuditorRequestResolutionAccepted> context, CancellationToken ct)
    {
        var accepted = context.Trigger;
        var tenant = Uuid.Parse(context.Source.Stream.Realm, CultureInfo.InvariantCulture);
        IReadOnlyList<AdmittedRequestChild> children;
        ClosureIntent intent;
        AuditorResolutionDecision decision;
        try
        {
            intent = await AuditorContent.ReadAsync<ClosureIntent>(content, tenant, accepted.Intent, ct)
                .ConfigureAwait(false);
            decision = await AuditorContent.ReadAsync<AuditorResolutionDecision>(content, tenant, intent.AuthoredDecision,
                ct).ConfigureAwait(false);
            children = await AuditorRequestResolutionProofs.ChildrenAsync(content, tenant, intent, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The frozen resolution cannot be verified yet: {exception.Message}", true));
        }

        _ = await process.StartAsync(AuditorRequestResolutionEffects.ProcessName, context, accepted.Intent,
            AuditorRequestResolutionProcess.ResponseKeys(tenant, decision.ProgramId, intent.ParentId, children),
            $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
    }
}
