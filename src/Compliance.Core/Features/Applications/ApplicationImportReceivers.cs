using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     The eight receivers of <c>ApplicationImportExecutionProcess</c>. Each verifies its proven source fact against
///     the authoritative batch and the frozen plan, resolves an exact replay from its own owner's accepted facts and
///     appends one fact (or one native batch for a target group). A fence recorded on a target wins over a delayed
///     operation; a transient wait is a transient conflict; a changed intent or invalid proof is a permanent
///     conflict; missing state is never proof to release anything. A target never consults the live batch to change
///     its own state: the batch is read only to prove the fact a receiver was handed.
/// </summary>
sealed class ApplicationImportReceivers(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, ApplicationImportVerifier verifier, ITenantActivity tenants, TimeProvider clock, IDomainEventReader events,
    IApplicationImportRetirementImpactReader? impactReader = null)
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> ClaimAsync(IExecutionContext context,
        ClaimApplicationImportSource command, CancellationToken ct)
    {
        if (Malformed(command.CommandId, command.OperationId, command.Generation))
            return SourceUseReceiverSupport.Permanent("The claim names another generation or operation.");
        var native = await verifier.LoadNativeAsync(command.Target.Realm, command.BatchId, Uuid.Empty, ct)
            .ConfigureAwait(false);
        if (!native.IsSuccess)
            return Result<ReceiverOutcome>.Failure(native.Error!);
        var (tenant, batch) = (native.Value!.TenantId, native.Value.Batch);
        var plan = batch.Plan!;
        var stream = new ApplicationImportSource(tenant, plan.SourceKey, plan.SourceNamespace).Stream;
        if (!ApplicationImportVerifier.IsFact(command.Source, batch, plan) || command.Intent != plan.FrozenPlan ||
            command.OperationId != plan.Operation.RootOperationId || command.Target != stream ||
            command.SourceKey != plan.SourceKey || command.SourceNamespace != plan.SourceNamespace)
            return SourceUseReceiverSupport.Permanent("The claim differs from the frozen import plan.");
        var provenance = ApplicationImportEffects.Child(command.CommandId, nameof(ClaimApplicationImportSource),
            command.Source.EventId, ApplicationImportIntentBindings.HeaderOf(plan.Operation), stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        Result<ReceiverOutcome>? Early(ApplicationImportSource source, bool prepared)
        {
            var slot = source.Find(batch.Id);
            if (slot?.Admitted is { } admitted)
            {
                if (command.ExpectedRevision != admitted.Revision - 1 || admitted.Intent != command.Intent ||
                    admitted.Operation.RootOperationId != command.OperationId ||
                    admitted.Operation.CausationEventId != command.Source.EventId || admitted.Operation.FirstOwner != batch.Stream ||
                    AcceptedOperationBinding.Of(admitted.Operation with { LogicalId = command.CommandId }) != binding)
                    return SourceUseReceiverSupport.Permanent("The claim differs from its exact native source admission.");
            }
            else if (!prepared)
                return null;
            if (batch.IsCancelRequested || batch.IsEnded)
                return ApplicationImportVerifier.Ended(batch, "The batch ended before its source was claimed.");
            if (Decide(source.Acceptance, binding, source.Stream) is { } decided)
                return decided;
            if (slot is not null)
                return slot.Settled is not null
                    ? SourceUseReceiverSupport.Superseded("The source claim of this batch was settled before it was granted.",
                        NativeFactReference.Of(source.Stream, slot.Settled))
                    : SourceUseReceiverSupport.Replayed(NativeFactReference.Of(source.Stream, slot.Admitted!));
            if (source.ActiveBatchId is not null)
                return SourceUseReceiverSupport.Transient("Another batch holds this source. Retry after it settles.");
            return source.Revision != command.ExpectedRevision
                ? SourceUseReceiverSupport.Permanent("The source changed after this plan was frozen.")
                : null;
        }

        var preparation = await NativeReceiverPreparation.PrepareAsync<ApplicationImportSource, ApplicationImportContext>(reader,
            () => new ApplicationImportSource(tenant, plan.SourceKey, plan.SourceNamespace, command.CommandId, batch.Id),
            source => Early(source, false), async (_, token) =>
            {
                var loaded = await verifier.PrepareAsync(native.Value, token).ConfigureAwait(false);
                if (!loaded.IsSuccess)
                    return Result<ApplicationImportContext>.Failure(loaded.Error!);
                return command.ExpectedRevision != loaded.Value!.Plan.SourceRevision
                    ? Result<ApplicationImportContext>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The claim differs from the frozen import plan.", false))
                    : loaded;
            }, ct).ConfigureAwait(false);
        if (!preparation.IsSuccess)
            return Result<ReceiverOutcome>.Failure(preparation.Error!);
        if (preparation.Value!.Outcome is { } known)
            return known;
        return await AppendAsync(context, () => new ApplicationImportSource(tenant, plan.SourceKey,
                plan.SourceNamespace, command.CommandId, batch.Id), source => source.Acceptance,
            source => Early(source, true),
            (ApplicationImportSource source) => source.Claim(provenance, command.Intent, batch.Id), ct).ConfigureAwait(false);
    }

    public async ValueTask<Result<ReceiverOutcome>> ReserveAsync(IExecutionContext context,
        ReserveApplicationImportTarget command, CancellationToken ct)
    {
        if (Malformed(command.CommandId, command.OperationId, command.Generation))
            return SourceUseReceiverSupport.Permanent("The reservation names another generation or operation.");
        var native = await ReadTargetNativeAsync(command.Target, command.Source, command.BatchId, command.ApplicationId,
            command.OperationId, ct).ConfigureAwait(false);
        if (native.Failure is { } refused)
            return refused;
        var bound = await BindTargetRowsAsync(native.Context!, command.ApplicationId, command.Intent, command.GroupedRows, ct)
            .ConfigureAwait(false);
        if (!bound.IsSuccess)
            return Result<ReceiverOutcome>.Failure(bound.Error!);
        var (tenant, batch, group) = (native.Context!.TenantId, native.Context.Batch, bound.Value!);
        var provenance = ApplicationImportEffects.Child(command.CommandId, nameof(ReserveApplicationImportTarget),
            command.Source.EventId, ApplicationImportIntentBindings.HeaderOf(batch.Plan!.Operation), command.Target, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        Result<ReceiverOutcome>? Early(DeclaredApplication application, bool prepared)
        {
            var hold = application.FindImportHold(batch.Id);
            if (hold?.Reserved is { } reserved &&
                (reserved.Intent != command.Intent || reserved.InitialApplicationRevision != group.InitialApplicationRevision ||
                    reserved.CandidateContentRevision != group.CandidateContentRevision ||
                    reserved.Operation.RootOperationId != command.OperationId || reserved.Operation.CausationEventId != command.Source.EventId ||
                    reserved.Operation.FirstOwner != batch.Stream ||
                    AcceptedOperationBinding.Of(reserved.Operation with { LogicalId = command.CommandId }) != binding))
                return SourceUseReceiverSupport.Permanent("The target reservation has another exact native group binding.");
            if (Decide(application.Acceptance, binding, application.Stream) is { } decided)
                return decided;
            if (hold?.Reserved is not null)
                return hold.Settled is not null || hold.Fence is not null
                    ? SourceUseReceiverSupport.Superseded("The target was fenced or settled before it was reserved.",
                        NativeFactReference.Of(application.Stream, (DomainEvent?)hold.Settled ?? hold.Fence!))
                    : SourceUseReceiverSupport.Replayed(NativeFactReference.Of(application.Stream, hold.Reserved));
            if (!prepared)
                return null;
            if (hold is not null)
                return SourceUseReceiverSupport.Superseded("The target was fenced or settled before it was reserved.",
                    NativeFactReference.Of(application.Stream, (DomainEvent?)hold.Settled ?? hold.Fence!));
            return application.CheckReserve(batch.Id, group.InitialApplicationRevision) is { } error
                ? Result<ReceiverOutcome>.Failure(error) : null;
        }
        var preparation = await NativeReceiverPreparation.PrepareAsync<DeclaredApplication, PreparedTarget>(reader,
            () => new DeclaredApplication(tenant, command.ApplicationId, command.CommandId),
            application => Early(application, false),
            (_, token) => PrepareTargetAsync(command.Target, command.Source, command.BatchId, command.ApplicationId,
                command.OperationId, command.Intent, command.GroupedRows, token), ct).ConfigureAwait(false);
        if (!preparation.IsSuccess)
            return Result<ReceiverOutcome>.Failure(preparation.Error!);
        if (preparation.Value!.Outcome is { } known)
            return known;
        if (preparation.Value.Value!.Outcome is { } terminal)
            return terminal;
        return await AppendAsync(context, () => new DeclaredApplication(tenant, command.ApplicationId, command.CommandId),
            application => application.Acceptance, application => Early(application, true),
            (DeclaredApplication application) => application.Reserve(provenance, command.Intent, batch.Id, command.ApplicationId,
                group.InitialApplicationRevision, group.CandidateContentRevision), ct).ConfigureAwait(false);
    }

    public async ValueTask<Result<ReceiverOutcome>> ApplyAsync(IExecutionContext context,
        ApplyApplicationImportEffect command, CancellationToken ct)
    {
        if (Malformed(command.CommandId, command.OperationId, command.Generation))
            return SourceUseReceiverSupport.Permanent("The apply names another generation or operation.");
        var native = await ReadTargetNativeAsync(command.Target, command.Source, command.BatchId, command.ApplicationId,
            command.OperationId, ct).ConfigureAwait(false);
        if (native.Failure is { } refused)
            return refused;
        var bound = await BindTargetRowsAsync(native.Context!, command.ApplicationId, command.Intent, command.RowGroup, ct)
            .ConfigureAwait(false);
        if (!bound.IsSuccess)
            return Result<ReceiverOutcome>.Failure(bound.Error!);
        var (tenant, batch, group) = (native.Context!.TenantId, native.Context.Batch, bound.Value!);
        var active = await tenants.IsActiveAsync(tenant, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        if (!active)
            return SourceUseReceiverSupport.Transient("The tenant is not active; retry after reactivation.");
        var accepted = batch.Plan!;
        var provenance = ApplicationImportEffects.Child(command.CommandId, nameof(ApplyApplicationImportEffect),
            command.Source.EventId, ApplicationImportIntentBindings.HeaderOf(accepted.Operation), command.Target, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        Result<ReceiverOutcome>? Early(DeclaredApplication application, bool prepared)
        {
            var hold = application.FindImportHold(batch.Id);
            if (hold is { IsApplied: true })
            {
                var effect = hold.Effects[0];
                if (effect.Intent != command.Intent || effect.FrozenPlan != accepted.FrozenPlan ||
                    effect.PlanRevision != accepted.Revision || effect.PlanSha256 != accepted.PlanSha256 ||
                    effect.Operation.RootOperationId != command.OperationId || effect.Operation.CausationEventId != command.Source.EventId ||
                    effect.Operation.FirstOwner != batch.Stream ||
                    AcceptedOperationBinding.Of(effect.Operation with { LogicalId = command.CommandId }) != binding)
                    return SourceUseReceiverSupport.Permanent("The target effect has another exact native group binding.");
            }
            if (Decide(application.Acceptance, binding, application.Stream) is { } decided)
                return decided;
            if (hold is { IsApplied: true, Fence: null, Settled: null })
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(application.Stream, hold.Effects[0]));
            if (!prepared)
                return null;
            if (hold is null)
                return SourceUseReceiverSupport.Transient("The target reservation is not visible yet.");
            if (hold.Fence is not null || hold.Settled is not null)
                return SourceUseReceiverSupport.Superseded("The target was fenced or settled; the delayed apply does nothing.",
                    NativeFactReference.Of(application.Stream, (DomainEvent?)hold.Settled ?? hold.Fence!));
            if (hold.Reserved is null || hold.Reserved.InitialApplicationRevision != group.InitialApplicationRevision ||
                hold.Reserved.CandidateContentRevision != group.CandidateContentRevision)
                return SourceUseReceiverSupport.Permanent("The target hold differs from the frozen group.");
            return null;
        }
        var preparation = await NativeReceiverPreparation.PrepareAsync<DeclaredApplication, PreparedApply>(reader,
            () => new DeclaredApplication(tenant, command.ApplicationId, command.CommandId),
            application => Early(application, false),
            (_, token) => PrepareApplyAsync(command, provenance, token), ct).ConfigureAwait(false);
        if (!preparation.IsSuccess)
            return Result<ReceiverOutcome>.Failure(preparation.Error!);
        if (preparation.Value!.Outcome is { } known)
            return known;
        var prepared = preparation.Value.Value!;
        if (prepared.Outcome is { } terminal)
            return terminal;
        var frozen = prepared.Context!.Batch.Plan!;
        return await AppendAsync(context, () => new DeclaredApplication(tenant, command.ApplicationId, command.CommandId),
            application => application.Acceptance, application => Early(application, true),
            (DeclaredApplication application) => application.ApplyImportGroup(batch.Id, frozen.Revision, frozen.PlanSha256,
                frozen.FrozenPlan, command.Intent, prepared.Inputs!, prepared.RecordedAt), ct).ConfigureAwait(false);
    }

    public async ValueTask<Result<ReceiverOutcome>> CommitAsync(IExecutionContext context,
        CommitApplicationImportBatch command, CancellationToken ct)
    {
        if (Malformed(command.CommandId, command.OperationId, command.Generation))
            return SourceUseReceiverSupport.Permanent("The commit names another generation or operation.");
        var native = await verifier.LoadNativeAsync(command.Target.Realm, command.BatchId, command.CommandId, ct)
            .ConfigureAwait(false);
        if (!native.IsSuccess)
            return Result<ReceiverOutcome>.Failure(native.Error!);
        var (tenant, batch) = (native.Value!.TenantId, native.Value.Batch);
        var plan = batch.Plan!;
        if (command.Target != batch.Stream || command.Intent != plan.FrozenPlan || command.OperationId != plan.Operation.RootOperationId)
            return SourceUseReceiverSupport.Permanent("The commit differs from the frozen import plan.");
        if ((batch.IsCancelRequested || batch.IsEnded) && batch.Committed is null)
            return ApplicationImportVerifier.Ended(batch, "The batch ended before it published.");
        if (!ApplicationImportVerifier.IsFact(command.Source, batch, batch.Authorization))
            return SourceUseReceiverSupport.Transient("The execution authorization is not visible yet.");
        var provenance = ApplicationImportEffects.Child(command.CommandId, nameof(CommitApplicationImportBatch),
            command.Source.EventId, ApplicationImportIntentBindings.HeaderOf(plan.Operation), batch.Stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        Result<ReceiverOutcome>? Early(ApplicationImportBatch owner)
        {
            if (owner.Committed is { } committed &&
                (committed.Intent != command.Intent || committed.Operation.RootOperationId != command.OperationId ||
                    committed.Operation.CausationEventId != command.Source.EventId || committed.Operation.FirstOwner != batch.Stream ||
                    AcceptedOperationBinding.Of(committed.Operation with { LogicalId = command.CommandId }) != binding))
                return SourceUseReceiverSupport.Permanent("The publication has another exact native execution binding.");
            if (Decide(owner.Acceptance, binding, owner.Stream) is { } decided)
                return decided;
            if (owner.Committed is { } existing)
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(owner.Stream, existing));
            return owner.IsCancelRequested || owner.IsEnded
                ? ApplicationImportVerifier.Ended(owner, "The batch ended before it published.") : null;
        }
        var preparation = await NativeReceiverPreparation.PrepareAsync<ApplicationImportBatch, PreparedCommit>(reader,
            () => new ApplicationImportBatch(tenant, batch.Id, command.CommandId), Early,
            (owner, token) => PrepareCommitAsync(command, native.Value with { Batch = owner }, token), ct)
            .ConfigureAwait(false);
        if (!preparation.IsSuccess)
            return Result<ReceiverOutcome>.Failure(preparation.Error!);
        if (preparation.Value!.Outcome is { } known)
            return known;
        var prepared = preparation.Value.Value!;
        var frozen = prepared.Context.Plan;
        return await AppendAsync(context, () => new ApplicationImportBatch(tenant, batch.Id, command.CommandId),
            fresh => fresh.Acceptance, Early,
            (ApplicationImportBatch fresh) => fresh.Commit(provenance, command.Intent, command.TargetProofManifest,
                frozen.Rows.Content, frozen.SourceRevision, prepared.RecordedAt, prepared.Result), ct).ConfigureAwait(false);
    }

    public async ValueTask<Result<ReceiverOutcome>> FenceAsync(IExecutionContext context,
        FenceApplicationImportTarget command, CancellationToken ct)
    {
        if (Malformed(command.CommandId, command.OperationId, command.Generation))
            return SourceUseReceiverSupport.Permanent("The fence names another generation or operation.");
        var native = await ReadCancellationNativeAsync(command.Target.Realm, command.Source, command.BatchId,
            command.OperationId, Uuid.Empty, ct).ConfigureAwait(false);
        if (native.Failure is { } refused)
            return refused;
        var (tenant, batch) = (native.TenantId, native.Batch!);
        if (command.Target != new DeclaredApplication(tenant, command.ApplicationId).Stream || batch.Plan is null)
            return SourceUseReceiverSupport.Permanent("The fence names another target or an unsealed batch with no targets.");
        var provenance = ApplicationImportEffects.Child(command.CommandId, nameof(FenceApplicationImportTarget),
            command.Source.EventId, ApplicationImportIntentBindings.HeaderOf(batch.CancellationAccepted!.Operation),
            command.Target, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        Result<ReceiverOutcome>? Early(DeclaredApplication application, bool prepared)
        {
            var hold = application.FindImportHold(batch.Id);
            if (hold?.Fence is { } fence &&
                (fence.Intent != command.Intent || fence.Operation.RootOperationId != command.OperationId ||
                    fence.Operation.CausationEventId != command.Source.EventId || fence.Operation.FirstOwner != batch.Stream ||
                    AcceptedOperationBinding.Of(fence.Operation with { LogicalId = command.CommandId }) != binding))
                return SourceUseReceiverSupport.Permanent("The target fence has another exact native cancellation binding.");
            if (Decide(application.Acceptance, binding, application.Stream) is { } decided)
                return decided;
            if (hold?.Fence is { } existing)
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(application.Stream, existing));
            return prepared && hold?.Settled is { } settled
                ? SourceUseReceiverSupport.Superseded("The target was already settled.", NativeFactReference.Of(application.Stream, settled))
                : null;
        }
        var preparation = await NativeReceiverPreparation.PrepareAsync<DeclaredApplication, PreparedFence>(reader,
            () => new DeclaredApplication(tenant, command.ApplicationId, command.CommandId),
            application => Early(application, false), (_, token) => PrepareFenceAsync(command, batch, token), ct)
            .ConfigureAwait(false);
        if (!preparation.IsSuccess)
            return Result<ReceiverOutcome>.Failure(preparation.Error!);
        if (preparation.Value!.Outcome is { } known)
            return known;
        if (preparation.Value.Value!.Outcome is { } terminal)
            return terminal;
        return await AppendAsync(context, () => new DeclaredApplication(tenant, command.ApplicationId, command.CommandId),
            application => application.Acceptance, application => Early(application, true),
            (DeclaredApplication application) => application.Fence(provenance, command.Intent, batch.Id, command.ApplicationId), ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Result<ReceiverOutcome>> CompleteAsync(IExecutionContext context,
        CompleteApplicationImportCancellation command, CancellationToken ct)
    {
        if (Malformed(command.CommandId, command.OperationId, command.Generation))
            return SourceUseReceiverSupport.Permanent("The completion names another generation or operation.");
        var native = await ReadCancellationNativeAsync(command.Target.Realm, command.Source, command.BatchId,
            command.OperationId, command.CommandId, ct).ConfigureAwait(false);
        if (native.Failure is { } refused)
            return refused;
        var (tenant, batch) = (native.TenantId, native.Batch!);
        var accepted = batch.CancellationAccepted!;
        if (command.Target != batch.Stream || command.Intent != accepted.Intent)
            return SourceUseReceiverSupport.Permanent("The completion differs from the accepted cancellation.");
        // The native canceled fact does not bind this extra manifest. Preserve its original required typed read.
        IReadOnlyList<ApplicationImportFenceProof> fences;
        try
        {
            fences = await ApplicationImportContent.ReadAsync<IReadOnlyList<ApplicationImportFenceProof>>(content,
                tenant, command.TargetFenceManifest, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The cancellation cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
        var provenance = ApplicationImportEffects.Child(command.CommandId, nameof(CompleteApplicationImportCancellation),
            command.Source.EventId, ApplicationImportIntentBindings.HeaderOf(accepted.Operation),
            batch.Stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        Result<ReceiverOutcome>? Early(ApplicationImportBatch owner)
        {
            if (owner.Canceled is { } canceled &&
                (canceled.Intent != command.Intent || canceled.Operation.RootOperationId != command.OperationId ||
                    canceled.Operation.CausationEventId != command.Source.EventId || canceled.Operation.FirstOwner != batch.Stream ||
                    AcceptedOperationBinding.Of(canceled.Operation with { LogicalId = command.CommandId }) != binding))
                return SourceUseReceiverSupport.Permanent("The completed cancellation has another exact native root binding.");
            if (Decide(owner.Acceptance, binding, owner.Stream) is { } decided)
                return decided;
            if (owner.Committed is { } committed)
                return SourceUseReceiverSupport.Superseded("The batch already published; the cancellation recovers forward.",
                    NativeFactReference.Of(owner.Stream, committed));
            return owner.Canceled is { } existing
                ? SourceUseReceiverSupport.Replayed(NativeFactReference.Of(owner.Stream, existing)) : null;
        }
        var preparation = await NativeReceiverPreparation.PrepareAsync<ApplicationImportBatch, PreparedCancellation>(reader,
            () => new ApplicationImportBatch(tenant, batch.Id, command.CommandId), Early,
            (_, token) => PrepareCancellationAsync(command, batch, fences, token), ct).ConfigureAwait(false);
        if (!preparation.IsSuccess)
            return Result<ReceiverOutcome>.Failure(preparation.Error!);
        if (preparation.Value!.Outcome is { } known)
            return known;
        var prepared = preparation.Value.Value!;
        return await AppendAsync(context, () => new ApplicationImportBatch(tenant, batch.Id, command.CommandId),
            fresh => fresh.Acceptance, Early,
            (ApplicationImportBatch fresh) => fresh.CompleteCancellation(provenance, command.Intent, prepared.Reason,
                prepared.ActorMemberId, prepared.ActorDisplay, prepared.RecordedAt, prepared.Result), ct).ConfigureAwait(false);
    }

    public async ValueTask<Result<ReceiverOutcome>> ReleaseSourceAsync(IExecutionContext context,
        ReleaseApplicationImportSource command, CancellationToken ct)
    {
        if (Malformed(command.CommandId, command.OperationId, command.Generation))
            return SourceUseReceiverSupport.Permanent("The release names another generation or operation.");
        var verified = await VerifyTerminalNativeAsync(command.Target.Realm, command.BatchId, command.Source, command.Intent,
            command.OperationId, ct).ConfigureAwait(false);
        if (verified.Failure is not null)
            return verified.Failure.Value;
        var (batch, header, disposition) = (verified.Batch!, verified.Header!, verified.Disposition);
        var tenant = Uuid.Parse(command.Target.Realm, CultureInfo.InvariantCulture);
        var stream = new ApplicationImportSource(tenant, batch.SourceKey!, batch.SourceNamespace!).Stream;
        if (command.Target != stream)
            return SourceUseReceiverSupport.Permanent("The release names another source stream.");
        var provenance = ApplicationImportEffects.Child(command.CommandId, nameof(ReleaseApplicationImportSource),
            command.Source.EventId, header, stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        Result<ReceiverOutcome>? Early(ApplicationImportSource source)
        {
            if (source.Find(batch.Id)?.Settled is { } settled &&
                (settled.Intent != command.Intent || settled.Disposition != disposition ||
                    settled.Operation.RootOperationId != command.OperationId || settled.Operation.CausationEventId != command.Source.EventId ||
                    settled.Operation.FirstOwner != batch.Stream ||
                    AcceptedOperationBinding.Of(settled.Operation with { LogicalId = command.CommandId }) != binding))
                return SourceUseReceiverSupport.Permanent("The source settlement has another exact native terminal binding.");
            if (Decide(source.Acceptance, binding, source.Stream) is { } decided)
                return decided;
            return source.Find(batch.Id) is { Settled: { } existing }
                ? SourceUseReceiverSupport.Replayed(NativeFactReference.Of(source.Stream, existing))
                : null;
        }

        var preparation = await NativeReceiverPreparation.PrepareAsync<ApplicationImportSource, ImportFrozenPlanIntent>(reader,
            () => new ApplicationImportSource(tenant, batch.SourceKey!, batch.SourceNamespace!, command.CommandId, batch.Id),
            Early, (_, token) => PrepareTerminalAsync(tenant, batch, command.Intent, command.OperationId, token), ct)
            .ConfigureAwait(false);
        if (!preparation.IsSuccess)
            return Result<ReceiverOutcome>.Failure(preparation.Error!);
        if (preparation.Value!.Outcome is { } known)
            return known;
        return await AppendAsync(context, () => new ApplicationImportSource(tenant, batch.SourceKey!,
                batch.SourceNamespace!, command.CommandId, batch.Id), source => source.Acceptance, Early,
            (ApplicationImportSource source) => source.Settle(provenance, command.Intent, batch.Id, disposition), ct).ConfigureAwait(false);
    }

    public async ValueTask<Result<ReceiverOutcome>> ReleaseTargetAsync(IExecutionContext context,
        ReleaseApplicationImportTarget command, CancellationToken ct)
    {
        if (Malformed(command.CommandId, command.OperationId, command.Generation))
            return SourceUseReceiverSupport.Permanent("The release names another generation or operation.");
        var verified = await VerifyTerminalNativeAsync(command.Target.Realm, command.BatchId, command.Source, command.Intent,
            command.OperationId, ct).ConfigureAwait(false);
        if (verified.Failure is not null)
            return verified.Failure.Value;
        var (batch, header, disposition) = (verified.Batch!, verified.Header!, verified.Disposition);
        var tenant = Uuid.Parse(command.Target.Realm, CultureInfo.InvariantCulture);
        var stream = new DeclaredApplication(tenant, command.ApplicationId).Stream;
        if (command.Target != stream)
            return SourceUseReceiverSupport.Permanent("The release names another target stream.");
        var provenance = ApplicationImportEffects.Child(command.CommandId, nameof(ReleaseApplicationImportTarget),
            command.Source.EventId, header, stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        Result<ReceiverOutcome>? Early(DeclaredApplication application, bool prepared)
        {
            if (application.FindImportHold(batch.Id)?.Settled is { } settled &&
                (settled.Intent != command.Intent || settled.Disposition != disposition ||
                    settled.Operation.RootOperationId != command.OperationId || settled.Operation.CausationEventId != command.Source.EventId ||
                    settled.Operation.FirstOwner != batch.Stream ||
                    AcceptedOperationBinding.Of(settled.Operation with { LogicalId = command.CommandId }) != binding))
                return SourceUseReceiverSupport.Permanent("The target settlement has another exact native terminal binding.");
            if (Decide(application.Acceptance, binding, application.Stream) is { } decided)
                return decided;
            var hold = application.FindImportHold(batch.Id);
            if (hold?.Settled is { } existing)
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(application.Stream, existing));
            if (!prepared)
                return null;
            if (hold is null)
                return disposition == ApplicationImportDisposition.Published
                    ? SourceUseReceiverSupport.Permanent("A published batch has no hold on this target.")
                    : SourceUseReceiverSupport.Transient("The target fence is not visible yet.");
            if (disposition == ApplicationImportDisposition.Published && !hold.IsApplied)
                return SourceUseReceiverSupport.Transient("The published target effects are not visible yet.");
            return disposition != ApplicationImportDisposition.Published && hold.Fence is null
                ? SourceUseReceiverSupport.Transient("The target fence is not visible yet.")
                : null;
        }

        var preparation = await NativeReceiverPreparation.PrepareAsync<DeclaredApplication, ImportFrozenPlanIntent>(reader,
            () => new DeclaredApplication(tenant, command.ApplicationId, command.CommandId),
            application => Early(application, false),
            (_, token) => PrepareTerminalAsync(tenant, batch, command.Intent, command.OperationId, token), ct)
            .ConfigureAwait(false);
        if (!preparation.IsSuccess)
            return Result<ReceiverOutcome>.Failure(preparation.Error!);
        if (preparation.Value!.Outcome is { } known)
            return known;
        return await AppendAsync(context, () => new DeclaredApplication(tenant, command.ApplicationId,
                command.CommandId), application => application.Acceptance, application => Early(application, true),
            (DeclaredApplication application) => application.Settle(provenance, command.Intent, batch.Id, command.ApplicationId, disposition),
            ct).ConfigureAwait(false);
    }

    async ValueTask<Result<PreparedCommit>> PrepareCommitAsync(CommitApplicationImportBatch command,
        ApplicationImportNativeContext native, CancellationToken ct)
    {
        var loaded = await verifier.PrepareAsync(native, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess)
            return Result<PreparedCommit>.Failure(loaded.Error!);
        try
        {
            var rows = await verifier.RowsAsync(loaded.Value!, ct).ConfigureAwait(false);
            if (!rows.IsSuccess)
                return Result<PreparedCommit>.Failure(rows.Error!);
            var proofs = await ApplicationImportContent.ReadAsync<IReadOnlyList<ApplicationImportEffectProof>>(content,
                native.TenantId, command.TargetProofManifest, ct).ConfigureAwait(false);
            if (await VerifyProofsAsync(loaded.Value!, rows.Value!, proofs, ct).ConfigureAwait(false) is { } proven)
                return Result<PreparedCommit>.Failure(proven.Error!);
            if (!await tenants.IsActiveAsync(native.TenantId, ct).ConfigureAwait(false))
                return Result<PreparedCommit>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The tenant is not active; retry after reactivation.", true));
            if (await VerifyRetirementsAsync(loaded.Value!, rows.Value!, ct).ConfigureAwait(false) is { } retirement)
                return Result<PreparedCommit>.Failure(retirement.Error!);
            var now = clock.GetUtcNow();
            var result = await ApplicationImportContent.UnitResultAsync(content, native.TenantId, command.OperationId, ct)
                .ConfigureAwait(false);
            return Result<PreparedCommit>.Success(new PreparedCommit(loaded.Value!, now, result));
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result<PreparedCommit>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The import publication cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<CancellationNativeVerification> ReadCancellationNativeAsync(string realm, NativeFactReference source,
        Uuid batchId, Uuid operationId, Uuid selectedLogicalId, CancellationToken ct)
    {
        var tenant = Uuid.Parse(realm, CultureInfo.InvariantCulture);
        var batch = await reader.HydrateAsync(new ApplicationImportBatch(tenant, batchId, selectedLogicalId), ct)
            .ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        if (!batch.IsCreated || batch.CancellationAccepted is not { } accepted ||
            !ApplicationImportVerifier.IsFact(source, batch, accepted))
            return new CancellationNativeVerification(tenant, null,
                SourceUseReceiverSupport.Transient("The accepted cancellation is not visible yet."));
        return operationId != accepted.Operation.RootOperationId
            ? new CancellationNativeVerification(tenant, null,
                SourceUseReceiverSupport.Permanent("The command differs from the actual native cancellation root."))
            : new CancellationNativeVerification(tenant, batch, null);
    }

    async ValueTask<Result<PreparedFence>> PrepareFenceAsync(FenceApplicationImportTarget command,
        ApplicationImportBatch batch, CancellationToken ct)
    {
        try
        {
            var verified = await VerifyCancellationTargetAsync(command.Target, command.Source, command.BatchId,
                command.ApplicationId, command.Intent, ct).ConfigureAwait(false);
            if (verified.Failure is { } failure)
                return failure.IsSuccess ? Result<PreparedFence>.Success(new PreparedFence(failure))
                    : Result<PreparedFence>.Failure(failure.Error!);
            ApplicationImportIntentBindings.Require(verified.Cancellation!.Header, batch.CancellationAccepted!.Operation);
            return command.OperationId != verified.Cancellation.Header.RootOperationId
                ? Result<PreparedFence>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The fence names another cancellation.", false))
                : Result<PreparedFence>.Success(new PreparedFence(null));
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result<PreparedFence>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The cancellation target cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<Result<PreparedCancellation>> PrepareCancellationAsync(CompleteApplicationImportCancellation command,
        ApplicationImportBatch batch, IReadOnlyList<ApplicationImportFenceProof> fences, CancellationToken ct)
    {
        var tenant = Uuid.Parse(command.Target.Realm, CultureInfo.InvariantCulture);
        try
        {
            var cancellation = await ApplicationImportContent.ReadAsync<ImportFrozenPlanIntent>(content, tenant,
                command.Intent, ct).ConfigureAwait(false);
            if (cancellation.BatchId != command.BatchId || cancellation.Header is null ||
                cancellation.Header.RootOperationId != command.OperationId)
                return Result<PreparedCancellation>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The completion differs from the accepted cancellation.", false));
            ApplicationImportIntentBindings.Require(cancellation.Header, batch.CancellationAccepted!.Operation);
            var expected = new List<Uuid>();
            if (batch.Plan is not null)
            {
                var sealedPlan = await verifier.LoadAsync(command.Target.Realm, command.BatchId, ct).ConfigureAwait(false);
                if (!sealedPlan.IsSuccess)
                    return Result<PreparedCancellation>.Failure(sealedPlan.Error!);
                var groups = await verifier.GroupsAsync(sealedPlan.Value!, ct).ConfigureAwait(false);
                if (!groups.IsSuccess)
                    return Result<PreparedCancellation>.Failure(groups.Error!);
                expected.AddRange(groups.Value!.Select(group => group.ApplicationId));
            }
            if (fences.Count != expected.Count || fences.Select(fence => fence.ApplicationId).Order().SequenceEqual(expected.Order()) is false)
                return Result<PreparedCancellation>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The fence manifest does not cover exactly the frozen targets.", false));
            foreach (var fence in fences)
            {
                var target = await reader.HydrateAsync(new DeclaredApplication(tenant, fence.ApplicationId), ct).ConfigureAwait(false);
                if (target.FindImportHold(batch.Id)?.Fence is not { Metadata: { } metadata } held ||
                    metadata.EventId != fence.EventId || metadata.AggregateVersion != fence.EventVersion)
                    return Result<PreparedCancellation>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "A target fence is not durable yet.", true));
                var actual = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, target, ct).ConfigureAwait(false);
                var stored = actual.SingleOrDefault(fact => fact.Metadata.EventId == fence.EventId);
                if (stored is null)
                    return Result<PreparedCancellation>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The exact target fence is not visible in its captured physical source prefix.", true));
                if (stored.GetType() != held.GetType() || stored.Metadata != held.Metadata ||
                    Kernel.ContentDigest.Sha256(stored, stored.GetType()) != Kernel.ContentDigest.Sha256(held, held.GetType()))
                    return Result<PreparedCancellation>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The cancellation fence differs from its exact committed source payload and metadata.", false));
            }
            var actor = cancellation.Header.OriginalActor;
            var actorMember = Uuid.TryParse(actor.Id, CultureInfo.InvariantCulture, out var memberId) ? memberId : Uuid.Empty;
            var now = clock.GetUtcNow();
            var result = await ApplicationImportContent.UnitResultAsync(content, tenant, command.OperationId, ct).ConfigureAwait(false);
            return Result<PreparedCancellation>.Success(new PreparedCancellation(cancellation.CancellationReason ?? string.Empty,
                actorMember, actor.Display, now, result));
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result<PreparedCancellation>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The cancellation cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<NativeTargetVerification> ReadTargetNativeAsync(EventStreamAddress target, NativeFactReference source,
        Uuid batchId, Uuid applicationId, Uuid operationId, CancellationToken ct)
    {
        var native = await verifier.LoadNativeAsync(target.Realm, batchId, Uuid.Empty, ct).ConfigureAwait(false);
        if (!native.IsSuccess)
            return new NativeTargetVerification(null, Result<ReceiverOutcome>.Failure(native.Error!));
        var batch = native.Value!.Batch;
        if (batch.IsCancelRequested || batch.IsEnded)
            return new NativeTargetVerification(null, ApplicationImportVerifier.Ended(batch, "The batch ended before this target step."));
        if (!ApplicationImportVerifier.IsFact(source, batch, batch.Authorization))
            return new NativeTargetVerification(null, SourceUseReceiverSupport.Transient("The execution authorization is not visible yet."));
        return operationId != batch.Plan!.Operation.RootOperationId || target != new DeclaredApplication(native.Value.TenantId, applicationId).Stream
            ? new NativeTargetVerification(null, SourceUseReceiverSupport.Permanent("The target differs from its exact native execution authorization."))
            : new NativeTargetVerification(native.Value, null);
    }

    async ValueTask<Result<ImportTargetGroupIntent>> BindTargetRowsAsync(ApplicationImportNativeContext native,
        Uuid applicationId, ImmutableContentRef intent, ImmutableContentRef rows, CancellationToken ct)
    {
        try
        {
            var group = await ApplicationImportContent.ReadAsync<ImportTargetGroupIntent>(content, native.TenantId, intent, ct)
                .ConfigureAwait(false);
            return group.BatchId != native.Batch.Id || group.ApplicationId != applicationId || group.Rows is null || group.Rows.Content != rows
                ? Result<ImportTargetGroupIntent>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The target rows differ from the frozen group intent.", false))
                : Result<ImportTargetGroupIntent>.Success(group);
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result<ImportTargetGroupIntent>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The required target-row binding cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<Result<PreparedTarget>> PrepareTargetAsync(EventStreamAddress target, NativeFactReference source,
        Uuid batchId, Uuid applicationId, Uuid operationId, ImmutableContentRef intent, ImmutableContentRef rows,
        CancellationToken ct)
    {
        try
        {
            var verified = await VerifyTargetAsync(target, source, batchId, applicationId, operationId, intent, ct)
                .ConfigureAwait(false);
            if (verified.Failure is { } failure)
                return failure.IsSuccess ? Result<PreparedTarget>.Success(new PreparedTarget(null, null, failure))
                    : Result<PreparedTarget>.Failure(failure.Error!);
            return rows != verified.Group!.Rows.Content
                ? Result<PreparedTarget>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The target differs from its frozen row group.", false))
                : Result<PreparedTarget>.Success(new PreparedTarget(verified.Context!, verified.Group, null));
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result<PreparedTarget>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The frozen target cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<Result<PreparedApply>> PrepareApplyAsync(ApplyApplicationImportEffect command,
        OperationProvenance provenance, CancellationToken ct)
    {
        var verified = await PrepareTargetAsync(command.Target, command.Source, command.BatchId, command.ApplicationId,
            command.OperationId, command.Intent, command.RowGroup, ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return Result<PreparedApply>.Failure(verified.Error!);
        if (verified.Value!.Outcome is { } terminal)
            return Result<PreparedApply>.Success(new PreparedApply(null, null, default, terminal));
        var (loaded, group) = (verified.Value.Context!, verified.Value.Group!);
        try
        {
            var rows = await ApplicationImportContent.ReadSelectionAsync<ApplicationImportPlannedRow>(content,
                loaded.TenantId, group.Rows, ct).ConfigureAwait(false);
            if (rows.Count == 0 || rows.Any(row => row.ApplicationId != command.ApplicationId))
                return Result<PreparedApply>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The frozen group rows name another application.", false));
            if (await VerifyRetirementsAsync(loaded, rows, ct).ConfigureAwait(false) is { } retirement)
                return Result<PreparedApply>.Failure(retirement.Error!);
            var planned = ApplicationImportGroupFold.Fold(group.InitialApplicationRevision != 0,
                false, (long)group.InitialApplicationRevision, rows);
            if (!planned.IsSuccess || (ulong)planned.Value![^1] != group.CandidateContentRevision)
                return Result<PreparedApply>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The frozen group differs from its candidate revision.", false));
            var inputs = new List<ApplicationImportEffectInput>(rows.Count);
            for (var index = 0; index < rows.Count; index++)
                inputs.Add(new ApplicationImportEffectInput(provenance with
                {
                    LogicalId = ApplicationImportEffects.EffectLogicalId(command.CommandId, index, rows[index].RowId),
                }, rows[index], planned.Value[index]));
            return Result<PreparedApply>.Success(new PreparedApply(loaded, inputs, clock.GetUtcNow(), null));
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result<PreparedApply>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The frozen group rows cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<TargetVerification> VerifyTargetAsync(EventStreamAddress target, NativeFactReference source,
        Uuid batchId, Uuid applicationId, Uuid operationId, ImmutableContentRef intent, CancellationToken ct)
    {
        var loaded = await verifier.LoadAsync(target.Realm, batchId, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess)
            return new TargetVerification(null, null, Result<ReceiverOutcome>.Failure(loaded.Error!));
        var batch = loaded.Value!.Batch;
        if (batch.IsCancelRequested || batch.IsEnded)
        {
            return new TargetVerification(loaded.Value, null,
                ApplicationImportVerifier.Ended(batch, "The batch ended before this target step."));
        }

        if (!ApplicationImportVerifier.IsFact(source, batch, batch.Authorization))
            return new TargetVerification(null, null,
                SourceUseReceiverSupport.Transient("The execution authorization is not visible yet."));
        var groups = await verifier.GroupsAsync(loaded.Value, ct).ConfigureAwait(false);
        if (!groups.IsSuccess)
            return new TargetVerification(null, null, Result<ReceiverOutcome>.Failure(groups.Error!));
        var group = groups.Value!.FirstOrDefault(candidate => candidate.ApplicationId == applicationId);
        var expectedStream = new DeclaredApplication(loaded.Value.TenantId, applicationId).Stream;
        if (group is null || target != expectedStream || operationId != loaded.Value.Plan.Header.RootOperationId ||
            group.BatchId != batchId)
            return new TargetVerification(null, null,
                SourceUseReceiverSupport.Permanent("The target is not a frozen group of this plan."));
        var groupRef = await ApplicationImportPlanWriter.GroupRefAsync(content, loaded.Value.TenantId,
            loaded.Value.PlanRef, group, ct).ConfigureAwait(false);
        return groupRef != intent
            ? new TargetVerification(null, null,
                SourceUseReceiverSupport.Permanent("The target intent differs from its frozen group."))
            : new TargetVerification(loaded.Value, group, null);
    }

    async ValueTask<CancellationVerification> VerifyCancellationTargetAsync(EventStreamAddress target,
        NativeFactReference source, Uuid batchId, Uuid applicationId, ImmutableContentRef intent, CancellationToken ct)
    {
        var tenant = Uuid.Parse(target.Realm, CultureInfo.InvariantCulture);
        var batch = await reader.HydrateAsync(new ApplicationImportBatch(tenant, batchId), ct).ConfigureAwait(false);
        if (!batch.IsCreated || batch.CancellationAccepted is not { } accepted ||
            !ApplicationImportVerifier.IsFact(source, batch, accepted))
            return new CancellationVerification(null, null,
                SourceUseReceiverSupport.Transient("The accepted cancellation is not visible yet."));
        ImportFrozenPlanIntent cancellation;
        try
        {
            cancellation = await ApplicationImportContent.ReadAsync<ImportFrozenPlanIntent>(content, tenant,
                accepted.Intent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return new CancellationVerification(null, null,
                Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                    $"The cancellation cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException)));
        }

        if (target != new DeclaredApplication(tenant, applicationId).Stream)
            return new CancellationVerification(null, null,
                SourceUseReceiverSupport.Permanent("The fence names another target stream."));
        if (batch.Plan is null)
            return new CancellationVerification(null, null,
                SourceUseReceiverSupport.Permanent("A batch with no sealed plan has no target to fence."));
        var loaded = await verifier.LoadAsync(target.Realm, batchId, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess)
            return new CancellationVerification(null, null, Result<ReceiverOutcome>.Failure(loaded.Error!));
        var groups = await verifier.GroupsAsync(loaded.Value!, ct).ConfigureAwait(false);
        if (!groups.IsSuccess)
            return new CancellationVerification(null, null, Result<ReceiverOutcome>.Failure(groups.Error!));
        var group = groups.Value!.FirstOrDefault(candidate => candidate.ApplicationId == applicationId);
        if (group is null)
            return new CancellationVerification(null, null,
                SourceUseReceiverSupport.Permanent("The target is not a frozen group of the sealed plan."));
        var groupRef = await ApplicationImportPlanWriter.GroupRefAsync(content, tenant, loaded.Value!.PlanRef, group, ct)
            .ConfigureAwait(false);
        return groupRef != intent
            ? new CancellationVerification(null, null,
                SourceUseReceiverSupport.Permanent("The fence intent differs from its frozen group."))
            : new CancellationVerification(loaded.Value, cancellation, null);
    }

    async ValueTask<TerminalVerification> VerifyTerminalNativeAsync(string realm, Uuid batchId, NativeFactReference source,
        ImmutableContentRef intent, Uuid operationId, CancellationToken ct)
    {
        var tenant = Uuid.Parse(realm, CultureInfo.InvariantCulture);
        var batch = await reader.HydrateAsync(new ApplicationImportBatch(tenant, batchId), ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        if (!batch.IsCreated || ApplicationImportVerifier.Terminal(batch) is not { } terminal ||
            !ApplicationImportVerifier.IsFact(source, batch, terminal.Fact))
            return new TerminalVerification(null, null, default,
                SourceUseReceiverSupport.Transient("The terminal batch fact is not visible yet."));
        var expectedIntent = batch.Canceled?.Intent ?? batch.Plan?.FrozenPlan;
        var root = batch.Canceled is not null ? batch.CancellationAccepted?.Operation : batch.Plan?.Operation;
        if (expectedIntent is null || intent != expectedIntent || root is null || operationId != root.RootOperationId)
            return new TerminalVerification(null, null, default,
                SourceUseReceiverSupport.Permanent("The release differs from the exact native terminal batch intent or root."));
        // Failed has no selected current producer. Preserve its original immutable binding; do not invent one.
        if (terminal.Disposition == ApplicationImportDisposition.Failed)
            return await VerifyTerminalAsync(realm, batchId, source, intent, operationId, ct).ConfigureAwait(false);
        return new TerminalVerification(batch, ApplicationImportIntentBindings.HeaderOf(root), terminal.Disposition, null);
    }

    async ValueTask<Result<ImportFrozenPlanIntent>> PrepareTerminalAsync(Uuid tenant, ApplicationImportBatch batch,
        ImmutableContentRef intent, Uuid operationId, CancellationToken ct)
    {
        try
        {
            var plan = await ApplicationImportContent.ReadAsync<ImportFrozenPlanIntent>(content, tenant, intent, ct)
                .ConfigureAwait(false);
            var root = batch.Canceled is not null ? batch.CancellationAccepted!.Operation : batch.Plan!.Operation;
            ApplicationImportIntentBindings.Require(plan.Header, root);
            return plan.Header.RootOperationId != operationId
                ? Result<ImportFrozenPlanIntent>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The release names another operation than the terminal batch.", false))
                : Result<ImportFrozenPlanIntent>.Success(plan);
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result<ImportFrozenPlanIntent>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The terminal intent cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<TerminalVerification> VerifyTerminalAsync(string realm, Uuid batchId, NativeFactReference source,
        ImmutableContentRef intent, Uuid operationId, CancellationToken ct)
    {
        var tenant = Uuid.Parse(realm, CultureInfo.InvariantCulture);
        var batch = await reader.HydrateAsync(new ApplicationImportBatch(tenant, batchId), ct).ConfigureAwait(false);
        if (!batch.IsCreated || ApplicationImportVerifier.Terminal(batch) is not { } terminal ||
            !ApplicationImportVerifier.IsFact(source, batch, terminal.Fact))
            return new TerminalVerification(null, null, default,
                SourceUseReceiverSupport.Transient("The terminal batch fact is not visible yet."));
        var expectedIntent = batch.Canceled?.Intent ?? batch.Plan?.FrozenPlan;
        if (expectedIntent is null || intent != expectedIntent)
            return new TerminalVerification(null, null, default,
                SourceUseReceiverSupport.Permanent("The release differs from the terminal batch intent."));
        try
        {
            var plan = await ApplicationImportContent.ReadAsync<ImportFrozenPlanIntent>(content, tenant, expectedIntent,
                ct).ConfigureAwait(false);
            return operationId != plan.Header.RootOperationId
                ? new TerminalVerification(null, null, default,
                    SourceUseReceiverSupport.Permanent("The release names another operation than the terminal batch."))
                : new TerminalVerification(batch, plan.Header, terminal.Disposition, null);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            ct.ThrowIfCancellationRequested();
            return new TerminalVerification(null, null, default,
                Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                    $"The terminal intent cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException)));
        }
    }

    /// <summary>Every native proof of the manifest names a durable, unfenced effect of exactly one frozen row.</summary>
    async ValueTask<Result<ReceiverOutcome>?> VerifyProofsAsync(ApplicationImportContext loaded,
        IReadOnlyList<ApplicationImportPlannedRow> rows, IReadOnlyList<ApplicationImportEffectProof> proofs,
        CancellationToken ct)
    {
        if (proofs.Count != rows.Count ||
            rows.Any(row => proofs.Count(proof => proof.RowId == row.RowId && proof.ApplicationId == row.ApplicationId) != 1) ||
            proofs.Select(proof => proof.EventId).Distinct().Count() != proofs.Count ||
            proofs.Any(proof => proof.EventId == Uuid.Empty || proof.EventVersion == 0))
            return SourceUseReceiverSupport.Permanent("The proof manifest does not cover exactly the frozen rows.");
        foreach (var applicationId in rows.Select(row => row.ApplicationId).Distinct())
        {
            var target = await reader.HydrateAsync(new DeclaredApplication(loaded.TenantId, applicationId), ct)
                .ConfigureAwait(false);
            if (target.FindImportHold(loaded.Batch.Id) is not { IsActive: true, Fence: null } hold)
                return SourceUseReceiverSupport.Transient("A target hold is not visible or was fenced.");
            IReadOnlyList<DomainEvent> actual;
            try
            {
                actual = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, target, ct).ConfigureAwait(false);
            }
            catch (ImmutableContentIntegrityException error)
            {
                return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                    $"The exact physical target source cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
            }
            foreach (var proof in proofs.Where(proof => proof.ApplicationId == applicationId))
            {
                var effect = hold.Effects.FirstOrDefault(candidate => candidate.Row.RowId == proof.RowId);
                if (effect?.Metadata is not { } metadata)
                    return SourceUseReceiverSupport.Transient("A target effect is not visible yet.");
                if (metadata.EventId != proof.EventId || metadata.AggregateVersion != proof.EventVersion ||
                    effect.Row != rows.First(row => row.RowId == proof.RowId))
                    return SourceUseReceiverSupport.Permanent("A target proof differs from its frozen row.");
                var stored = actual.SingleOrDefault(fact => fact.Metadata.EventId == proof.EventId);
                if (stored is null)
                    return SourceUseReceiverSupport.Transient("The exact target effect is not visible in its hydrated physical source cut yet.");
                if (stored is not ApplicationImportEffectPending || stored.Metadata != effect.Metadata ||
                    Kernel.ContentDigest.Sha256(stored, stored.GetType()) != Kernel.ContentDigest.Sha256(effect, effect.GetType()))
                    return SourceUseReceiverSupport.Permanent("The target effect differs from its exact committed source payload and metadata.");
            }
        }

        return null;
    }

    /// <summary>A retirement is applied and published only while its current impact still matches the frozen evidence.</summary>
    async ValueTask<Result<ReceiverOutcome>?> VerifyRetirementsAsync(ApplicationImportContext loaded,
        IReadOnlyList<ApplicationImportPlannedRow> rows, CancellationToken ct)
    {
        foreach (var row in rows.Where(row => row.Decision == "retire"))
        {
            if (!ApplicationImportRetirementImpact.IsValidFrozenRow(loaded.TenantId, loaded.Batch.Id,
                    loaded.Plan.SourceKey, loaded.Plan.SourceNamespace, row))
                return SourceUseReceiverSupport.Permanent("The frozen retirement row or its source proof is invalid.");
            if (impactReader is null || row.ExpectedApplicationRevision is not { } revision)
                return SourceUseReceiverSupport.Transient(
                    "Current complete retirement impact is unavailable; the import remains pending.");
            var impact = await impactReader.ReadForImportWorkerAsync(new PreviewApplicationChange(loaded.TenantId,
                row.ApplicationId, revision, "retire"), ct).ConfigureAwait(false);
            if (!impact.IsSuccess || !ApplicationImportRetirementImpact.MatchesFrozen(impact.Value, loaded.TenantId, row))
                return SourceUseReceiverSupport.Transient(
                    "Current complete retirement impact is unavailable; the import remains pending.");
        }

        return null;
    }

    async ValueTask<Result<ReceiverOutcome>> AppendAsync<TOwner>(IExecutionContext context, Func<TOwner> open,
        Func<TOwner, NativeAcceptance> acceptance, Func<TOwner, Result<ReceiverOutcome>?> decide,
        Func<TOwner, Result> raise, CancellationToken ct) where TOwner : Aggregate
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var owner = await reader.HydrateAsync(open(), ct).ConfigureAwait(false);
                if (decide(owner) is { } early)
                    return early;
                var step = await executor.ExecuteAsync(open(), fresh =>
                {
                    if (decide(fresh) is { } decided)
                        return AggregateOutcome.Discard(Result<Step>.Success(new Decided(decided)));
                    var raised = raise(fresh);
                    return raised.IsSuccess
                        ? AggregateOutcome.Commit(Result<Step>.Success(new Decided(SourceUseReceiverSupport.Applied(
                            NativeFactReference.Of(fresh.Stream, acceptance(fresh).SelectedFact!)))))
                        : AggregateOutcome.Discard(Result<Step>.Failure(raised.Error!));
                }, context, ct).ConfigureAwait(false);
                return step.IsSuccess
                    ? ((Decided)step.Value!).Outcome
                    : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The owner changed concurrently; rehydrate and decide again, including against a late fence.
            }
        }
    }

    static Result<ReceiverOutcome>? Decide(NativeAcceptance acceptance, AcceptedOperationBinding binding,
        EventStreamAddress stream) => acceptance.Decide(binding) switch
        {
            NativeAcceptanceDecision.Replayed => SourceUseReceiverSupport.Replayed(NativeFactReference.Of(stream,
                acceptance.SelectedFact!)),
            NativeAcceptanceDecision.Conflict => Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError),
            _ => null,
        };

    static bool Malformed(Uuid commandId, Uuid operationId, long generation) =>
        commandId == Uuid.Empty || operationId == Uuid.Empty || generation != ApplicationImportEffects.Generation;

    abstract record Step;

    sealed record Decided(Result<ReceiverOutcome> Outcome) : Step;

    sealed record PreparedCommit(ApplicationImportContext Context, DateTimeOffset RecordedAt, ImmutableContentRef Result);

    sealed record CancellationNativeVerification(Uuid TenantId, ApplicationImportBatch? Batch, Result<ReceiverOutcome>? Failure);

    sealed record PreparedFence(Result<ReceiverOutcome>? Outcome);

    sealed record PreparedCancellation(string Reason, Uuid ActorMemberId, string ActorDisplay, DateTimeOffset RecordedAt,
        ImmutableContentRef Result);

    sealed record NativeTargetVerification(ApplicationImportNativeContext? Context, Result<ReceiverOutcome>? Failure);

    sealed record PreparedTarget(ApplicationImportContext? Context, ImportTargetGroupIntent? Group,
        Result<ReceiverOutcome>? Outcome);

    sealed record PreparedApply(ApplicationImportContext? Context, IReadOnlyList<ApplicationImportEffectInput>? Inputs,
        DateTimeOffset RecordedAt, Result<ReceiverOutcome>? Outcome);

    sealed record TargetVerification(ApplicationImportContext? Context, ImportTargetGroupIntent? Group,
        Result<ReceiverOutcome>? Failure);

    sealed record CancellationVerification(ApplicationImportContext? Context, ImportFrozenPlanIntent? Cancellation,
        Result<ReceiverOutcome>? Failure);

    sealed record TerminalVerification(ApplicationImportBatch? Batch, DurableIntentHeader? Header,
        ApplicationImportDisposition Disposition, Result<ReceiverOutcome>? Failure);
}
