using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

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
            admission.AuthoredIntentSha256, admission.AcceptedAt, admission.SourceOwner, admission.SourceOwner);
        var binding = AcceptedOperationBinding.Of(provenance);
        for (var round = 1; round <= MaximumRounds; round++)
        {
            ct.ThrowIfCancellationRequested();
            var loaded = await guards.LoadAsync(admission.TenantId, admission.CommandId, ct).ConfigureAwait(false);
            if (loaded.Guard.Acceptance.Decide(binding) != NativeAcceptanceDecision.None)
                return await ReplayAsync(context, loaded, binding, realm, ct).ConfigureAwait(false);

            var bound = admission.Bind(loaded.Guard);
            if (!bound.IsSuccess)
                return Result<AdministratorTransitionResponse>.Failure(bound.Error!);
            var planned = loaded.Guard.Plan(admission.CommandKind, admission.MemberEpisodeId);
            if (!planned.IsSuccess)
                return Result<AdministratorTransitionResponse>.Failure(planned.Error!);
            var plan = planned.Value!;
            var intent = await WriteIntentAsync(admission, guardStream, plan, bound.Value!, realm, ct)
                .ConfigureAwait(false);
            var response = await AdministratorTransitionResponses.FreezeAcceptedAsync(content, realm,
                admission.CommandId, admission.AcceptedAt, ct).ConfigureAwait(false);
            var accepted = await operations.ExecuteAsync(new NativeOperation<TenantManagerGuard>(binding, loaded.Create,
                guard => guard.Acceptance,
                guard => Recheck(guard, admission, plan, bound.Value!, response),
                (TenantManagerGuard guard, ImmutableContentRef frozen) => guard.Admit(plan, admission.MemberEpisodeId, provenance, admission.SourceOwner,
                    admission.AuthoredRevision, intent, frozen, loaded.BaselineRef)), context, ct)
                .ConfigureAwait(false);
            if (!accepted.IsSuccess && accepted.Error == TenantManagerGuard.StaleConflict)
                continue;
            return await TeamMemberResponses.ReadAsync(content, realm, accepted, ct).ConfigureAwait(false);
        }

        return Result<AdministratorTransitionResponse>.Failure(TenantManagerGuard.StaleConflict);
    }

    static Result<FirstResponseDescriptor> Recheck(TenantManagerGuard guard, AdministratorAdmission admission,
        AdmissionPlan plan, AdministratorSourceBinding bound, FirstResponseDescriptor response)
    {
        var rebound = admission.Bind(guard);
        if (!rebound.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(rebound.Error!);
        var replanned = guard.Plan(admission.CommandKind, admission.MemberEpisodeId);
        if (!replanned.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(replanned.Error!);
        return replanned.Value != plan || rebound.Value != bound
            ? Result<FirstResponseDescriptor>.Failure(TenantManagerGuard.StaleConflict)
            : Result<FirstResponseDescriptor>.Success(response);
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
