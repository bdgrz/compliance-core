using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>
///     Admits one route-determined administrator transition as the first native fact on the tenant manager guard.
///     The guard is read only to choose which of its facts the route appends; the source owner is never mutated here.
///     The durable intent and the frozen 202 response are written before the append, a plan made on an older guard
///     tail is refused as stale and re-planned, and an exact retry replays the frozen response.
/// </summary>
public sealed class AdministratorTransitionAdmission(TenantManagerGuardLoader guards, NativeOperationExecutor operations,
    IImmutableContentStore content)
{
    /// <summary>Plan-write-append rounds before a lost guard race is surfaced to the caller as transient.</summary>
    const int MaximumRounds = 8;

    /// <summary>Admits the transition, or replays the response its logical identity already froze.</summary>
    public async ValueTask<Result<AdministratorTransitionResponse>> AdmitAsync(IExecutionContext context,
        AdministratorAdmission admission, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(admission);
        var realm = admission.TenantId.ToString();
        var guardStream = new TenantManagerGuard(admission.TenantId).Stream;
        var provenance = new OperationProvenance(admission.CommandId, admission.CommandId, null,
            admission.CommandKind, admission.Actor, guardStream, admission.MemberEpisodeId.ToString(),
            admission.AuthoredRevision >= 0 ? (ulong)admission.AuthoredRevision : null,
            admission.AuthoredIntentSha256, admission.AcceptedAt, admission.SourceOwner, admission.SourceOwner, FirstOwner: guardStream);
        for (var round = 1; round <= MaximumRounds; round++)
        {
            ct.ThrowIfCancellationRequested();
            var loaded = await guards.LoadAsync(admission.TenantId, admission.CommandId, admission.MemberEpisodeId, admission.CommandId, ct).ConfigureAwait(false);
            // A system command authored with the current-episode sentinel resolves its accepted episode from
            // the original fact. The authored digest still distinguishes changed episode inputs and intent.
            var retryProvenance = admission.ResolveCurrentEpisode &&
                                  loaded.Guard.Acceptance.SelectedOperation is { } original
                ? provenance with { Episode = original.Operation.Episode }
                : provenance;
            var binding = AcceptedOperationBinding.Of(retryProvenance);
            if (loaded.Guard.Acceptance.Decide(binding) != NativeAcceptanceDecision.None)
                return await ReplayAsync(context, loaded, binding, realm, ct).ConfigureAwait(false);

            NativePreparedCut? preparedCut = null;
            AdmissionPlan? plan = null;
            ImmutableContentRef? intent = null;
            var baselineRef = loaded.BaselineRef;
            var accepted = await operations.ExecuteAsync(new NativeOperation<TenantManagerGuard>(binding, loaded.Create,
                guard => guard.Acceptance,
                async (guard, token) =>
                {
                    preparedCut = NativePreparedCut.Of(guard, guard.ReplayCut.Digest, guard.ReplayCut.MetadataDigest);
                    var bound = admission.Bind(guard);
                    if (!bound.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(bound.Error!);
                    var planned = guard.Plan(admission.CommandKind, admission.MemberEpisodeId);
                    if (!planned.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(planned.Error!);
                    plan = planned.Value!;
                    if (guard.IsEmpty)
                        baselineRef = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), GuardBaseline.Schema,
                            loaded.Baseline, token).ConfigureAwait(false);
                    intent = await WriteIntentAsync(admission, guardStream, plan, bound.Value!, realm, token).ConfigureAwait(false);
                    return Result<FirstResponseDescriptor>.Success(await AdministratorTransitionResponses.FreezeAcceptedAsync(content,
                        realm, admission.CommandId, admission.AcceptedAt, token).ConfigureAwait(false));
                },
                (TenantManagerGuard guard, ImmutableContentRef frozen) =>
                    preparedCut is not null && preparedCut == NativePreparedCut.Of(guard, guard.ReplayCut.Digest, guard.ReplayCut.MetadataDigest)
                        ? guard.Admit(plan!, admission.MemberEpisodeId, admission.MemberId, retryProvenance,
                            admission.SourceOwner, admission.AuthoredRevision, intent!, frozen, baselineRef)
                        : Result.Failure(TenantManagerGuard.StaleConflict)), context, ct)
                .ConfigureAwait(false);
            if (!accepted.IsSuccess && accepted.Error == TenantManagerGuard.StaleConflict)
                continue;
            return await TeamMemberResponses.ReadAsync(content, realm, accepted, ct).ConfigureAwait(false);
        }

        return Result<AdministratorTransitionResponse>.Failure(TenantManagerGuard.StaleConflict);
    }

    async ValueTask<Result<AdministratorTransitionResponse>> ReplayAsync(IExecutionContext context, LoadedGuard loaded,
        AcceptedOperationBinding binding, string realm, CancellationToken ct)
    {
        var accepted = await operations.ExecuteAsync(new NativeOperation<TenantManagerGuard>(binding, loaded.Create,
            guard => guard.Acceptance,
            _ => Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The accepted operation could not be replayed.")),
            (_, _) => Result.Failure(AcceptedAppendResult.ConflictError)), context, ct).ConfigureAwait(false);
        return await TeamMemberResponses.ReadAsync(content, realm, accepted, ct).ConfigureAwait(false);
    }

    async Task<ImmutableContentRef> WriteIntentAsync(AdministratorAdmission admission, EventStreamAddress guardStream,
        AdmissionPlan plan, AdministratorSourceBinding bound, string realm, CancellationToken ct)
    {
        var eligibility = await WriteEligibilityRequirementsAsync(realm, ct).ConfigureAwait(false);
        var header = new DurableIntentHeader(admission.CommandId, guardStream, admission.SourceOwner,
            admission.SourceOwner, admission.Actor, admission.MemberEpisodeId.ToString(),
            admission.AuthoredRevision >= 0 ? (ulong)admission.AuthoredRevision : null,
            admission.AuthoredIntentSha256, admission.AcceptedAt);
        var intent = new AdministratorTransitionIntent(header, admission.MemberId, admission.MemberEpisodeId,
            bound.MemberRevision, admission.AssignmentId, bound.AssignmentGeneration, bound.ExpectedAssignmentRevision,
            plan.Kind, bound.Reason, (ulong)plan.Generation, eligibility);
        return await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), AdministratorTransitionIntent.Schema, intent,
            ct).ConfigureAwait(false);
    }

    /// <summary>The eligibility is read from the source owners at completion, so the frozen requirement set is empty.</summary>
    async Task<ImmutableSelectionManifest> WriteEligibilityRequirementsAsync(string realm, CancellationToken ct)
    {
        var items = System.Text.Encoding.UTF8.GetBytes("[]");
        var reference = await content.WriteVerifiedAsync(realm, Uuid.CreateVersion4(), ItemSchema,
            ImmutableContentStoreExtensions.JsonMediaType, items, ct).ConfigureAwait(false);
        return new ImmutableSelectionManifest(reference, ItemSchema, 0, SelectionOrder.CanonicalKey,
            ContentDigest.Sha256(items));
    }

    const string ItemSchema = "source-use-requirement.v1";
}
