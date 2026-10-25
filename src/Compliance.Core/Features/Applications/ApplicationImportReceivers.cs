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
        var loaded = await verifier.LoadAsync(command.Target.Realm, command.BatchId, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess)
            return Result<ReceiverOutcome>.Failure(loaded.Error!);
        var (tenant, batch, plan) = (loaded.Value!.TenantId, loaded.Value.Batch, loaded.Value.Plan);
        var stream = new ApplicationImportSource(tenant, plan.SourceKey, plan.SourceNamespace).Stream;
        if (!ApplicationImportVerifier.IsFact(command.Source, batch, batch.Plan) || command.Intent != loaded.Value.PlanRef ||
            command.OperationId != plan.Header.RootOperationId || command.Target != stream ||
            command.SourceKey != plan.SourceKey || command.SourceNamespace != plan.SourceNamespace ||
            command.ExpectedRevision != plan.SourceRevision)
            return SourceUseReceiverSupport.Permanent("The claim differs from the frozen import plan.");
        if (batch.IsCancelRequested || batch.IsEnded)
            return ApplicationImportVerifier.Ended(batch, "The batch ended before its source was claimed.");

        var provenance = ApplicationImportEffects.Child(command.CommandId, nameof(ClaimApplicationImportSource),
            command.Source.EventId, plan, stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        Result<ReceiverOutcome>? Early(ApplicationImportSource source)
        {
            if (Decide(source.Acceptance, binding, source.Stream) is { } decided)
                return decided;
            if (source.Find(batch.Id) is { } slot)
                return slot.Settled is not null
                    ? SourceUseReceiverSupport.Superseded("The source claim of this batch was settled before it was granted.",
                        NativeFactReference.Of(source.Stream, slot.Settled))
                    : SourceUseReceiverSupport.Replayed(NativeFactReference.Of(source.Stream, slot.Admitted!));
            if (source.ActiveBatchId is not null)
                return SourceUseReceiverSupport.Transient("Another batch holds this source. Retry after it settles.");
            return source.Revision != plan.SourceRevision
                ? SourceUseReceiverSupport.Permanent("The source changed after this plan was frozen.")
                : null;
        }

        return await AppendAsync(context, () => new ApplicationImportSource(tenant, plan.SourceKey,
            plan.SourceNamespace, command.CommandId, batch.Id), source => source.Acceptance, Early,
            (ApplicationImportSource source) => source.Claim(provenance, command.Intent, batch.Id), ct).ConfigureAwait(false);
    }

    public async ValueTask<Result<ReceiverOutcome>> ReserveAsync(IExecutionContext context,
        ReserveApplicationImportTarget command, CancellationToken ct)
    {
        if (Malformed(command.CommandId, command.OperationId, command.Generation))
            return SourceUseReceiverSupport.Permanent("The reservation names another generation or operation.");
        var verified = await VerifyTargetAsync(command.Target, command.Source, command.BatchId, command.ApplicationId,
            command.OperationId, command.Intent, ct).ConfigureAwait(false);
        if (verified.Failure is not null)
            return verified.Failure.Value;
        var (loaded, group) = (verified.Context!, verified.Group!);
        if (command.GroupedRows != group.Rows.Content)
            return SourceUseReceiverSupport.Permanent("The reservation differs from its frozen target group.");
        var batch = loaded.Batch;
        if (batch.IsCancelRequested || batch.IsEnded)
            return ApplicationImportVerifier.Ended(batch, "The batch ended before its target was reserved.");

        var provenance = ApplicationImportEffects.Child(command.CommandId, nameof(ReserveApplicationImportTarget),
            command.Source.EventId, loaded.Plan, command.Target, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        Result<ReceiverOutcome>? Early(DeclaredApplication application)
        {
            if (Decide(application.Acceptance, binding, application.Stream) is { } decided)
                return decided;
            if (application.FindImportHold(batch.Id) is { } hold)
            {
                if (hold.Settled is not null || hold.Fence is not null)
                    return SourceUseReceiverSupport.Superseded("The target was fenced or settled before it was reserved.",
                        NativeFactReference.Of(application.Stream, (DomainEvent?)hold.Settled ?? hold.Fence!));
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(application.Stream, hold.Reserved!));
            }

            return application.CheckReserve(batch.Id, group.InitialApplicationRevision) is { } refused
                ? Result<ReceiverOutcome>.Failure(refused)
                : null;
        }

        return await AppendAsync(context, () => new DeclaredApplication(loaded.TenantId, command.ApplicationId,
                command.CommandId), application => application.Acceptance, Early,
            (DeclaredApplication application) => application.Reserve(provenance, command.Intent, batch.Id, command.ApplicationId,
                group.InitialApplicationRevision, group.CandidateContentRevision), ct).ConfigureAwait(false);
    }

    public async ValueTask<Result<ReceiverOutcome>> ApplyAsync(IExecutionContext context,
        ApplyApplicationImportEffect command, CancellationToken ct)
    {
        if (Malformed(command.CommandId, command.OperationId, command.Generation))
            return SourceUseReceiverSupport.Permanent("The apply names another generation or operation.");
        var verified = await VerifyTargetAsync(command.Target, command.Source, command.BatchId, command.ApplicationId,
            command.OperationId, command.Intent, ct).ConfigureAwait(false);
        if (verified.Failure is not null)
            return verified.Failure.Value;
        var (loaded, group) = (verified.Context!, verified.Group!);
        if (command.RowGroup != group.Rows.Content)
            return SourceUseReceiverSupport.Permanent("The apply differs from its frozen target group.");
        var batch = loaded.Batch;
        if (batch.IsCancelRequested || batch.IsEnded)
            return ApplicationImportVerifier.Ended(batch, "The batch ended before its target was applied.");
        if (!await tenants.IsActiveAsync(loaded.TenantId, ct).ConfigureAwait(false))
            return SourceUseReceiverSupport.Transient("The tenant is not active; retry after reactivation.");

        IReadOnlyList<ApplicationImportPlannedRow> rows;
        try
        {
            rows = await ApplicationImportContent.ReadSelectionAsync<ApplicationImportPlannedRow>(content,
                loaded.TenantId, group.Rows, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                    $"The frozen group rows cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        if (rows.Count == 0 || rows.Any(row => row.ApplicationId != command.ApplicationId))
            return SourceUseReceiverSupport.Permanent("The frozen group rows name another application.");
        var retirements = await VerifyRetirementsAsync(loaded, rows, ct).ConfigureAwait(false);
        if (retirements is not null)
            return retirements.Value;

        var provenance = ApplicationImportEffects.Child(command.CommandId, nameof(ApplyApplicationImportEffect),
            command.Source.EventId, loaded.Plan, command.Target, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var inputs = new List<ApplicationImportEffectInput>(rows.Count);
        var planned = ApplicationImportGroupFold.Fold(group.InitialApplicationRevision != 0,
            false, (long)group.InitialApplicationRevision, rows);
        if (!planned.IsSuccess || (ulong)planned.Value![^1] != group.CandidateContentRevision)
            return SourceUseReceiverSupport.Permanent("The frozen group differs from its candidate revision.");
        var now = clock.GetUtcNow();
        for (var index = 0; index < rows.Count; index++)
            inputs.Add(new ApplicationImportEffectInput(provenance with
            {
                LogicalId = ApplicationImportEffects.EffectLogicalId(command.CommandId, index, rows[index].RowId),
            }, rows[index], planned.Value[index]));

        Result<ReceiverOutcome>? Early(DeclaredApplication application)
        {
            if (Decide(application.Acceptance, binding, application.Stream) is { } decided)
                return decided;
            var hold = application.FindImportHold(batch.Id);
            if (hold is null)
                return SourceUseReceiverSupport.Transient("The target reservation is not visible yet.");
            if (hold.Fence is not null || hold.Settled is not null)
                return SourceUseReceiverSupport.Superseded(
                    "The target was fenced or settled; the delayed apply does nothing.",
                    NativeFactReference.Of(application.Stream, (DomainEvent?)hold.Settled ?? hold.Fence!));
            if (hold.IsApplied)
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(application.Stream, hold.Effects[0]));
            if (hold.Reserved is null || hold.Reserved.InitialApplicationRevision != group.InitialApplicationRevision ||
                hold.Reserved.CandidateContentRevision != group.CandidateContentRevision)
                return SourceUseReceiverSupport.Permanent("The target hold differs from the frozen group.");
            return null;
        }

        return await AppendAsync(context, () => new DeclaredApplication(loaded.TenantId, command.ApplicationId,
                command.CommandId), application => application.Acceptance, Early,
            (DeclaredApplication application) => application.ApplyImportGroup(batch.Id, batch.Plan!.Revision, batch.Plan.PlanSha256,
                loaded.PlanRef, command.Intent, inputs, now), ct).ConfigureAwait(false);
    }

    public async ValueTask<Result<ReceiverOutcome>> CommitAsync(IExecutionContext context,
        CommitApplicationImportBatch command, CancellationToken ct)
    {
        if (Malformed(command.CommandId, command.OperationId, command.Generation))
            return SourceUseReceiverSupport.Permanent("The commit names another generation or operation.");
        var loaded = await verifier.LoadAsync(command.Target.Realm, command.BatchId, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess)
            return Result<ReceiverOutcome>.Failure(loaded.Error!);
        var (tenant, batch, plan) = (loaded.Value!.TenantId, loaded.Value.Batch, loaded.Value.Plan);
        if (command.Target != batch.Stream || command.Intent != loaded.Value.PlanRef ||
            command.OperationId != plan.Header.RootOperationId)
            return SourceUseReceiverSupport.Permanent("The commit differs from the frozen import plan.");
        var provenance = ApplicationImportEffects.Child(command.CommandId, nameof(CommitApplicationImportBatch),
            command.Source.EventId, plan, batch.Stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        if (batch.Acceptance.SelectedLogicalId != command.CommandId)
            batch = await reader.HydrateAsync(new ApplicationImportBatch(tenant, batch.Id, command.CommandId), ct)
                .ConfigureAwait(false);
        if (Decide(batch.Acceptance, binding, batch.Stream) is { } early)
            return early;
        if (batch.IsCancelRequested || batch.IsEnded)
            return ApplicationImportVerifier.Ended(batch, "The batch ended before it published.");
        if (!ApplicationImportVerifier.IsFact(command.Source, batch, batch.Authorization))
            return SourceUseReceiverSupport.Transient("The execution authorization is not visible yet.");

        var rows = await verifier.RowsAsync(loaded.Value, ct).ConfigureAwait(false);
        if (!rows.IsSuccess)
            return Result<ReceiverOutcome>.Failure(rows.Error!);
        IReadOnlyList<ApplicationImportEffectProof> proofs;
        try
        {
            proofs = await ApplicationImportContent.ReadAsync<IReadOnlyList<ApplicationImportEffectProof>>(content,
                tenant, command.TargetProofManifest, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                    $"The target proof manifest cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        var proven = await VerifyProofsAsync(loaded.Value, rows.Value!, proofs, ct).ConfigureAwait(false);
        if (proven is not null)
            return proven.Value;
        if (!await tenants.IsActiveAsync(tenant, ct).ConfigureAwait(false))
            return SourceUseReceiverSupport.Transient("The tenant is not active; retry after reactivation.");
        var retirements = await VerifyRetirementsAsync(loaded.Value, rows.Value!, ct).ConfigureAwait(false);
        if (retirements is not null)
            return retirements.Value;

        var now = clock.GetUtcNow();
        var operationResult = await ApplicationImportContent.UnitResultAsync(content, tenant, provenance.RootOperationId, ct)
            .ConfigureAwait(false);
        return await AppendAsync(context, () => new ApplicationImportBatch(tenant, batch.Id, command.CommandId),
            fresh => fresh.Acceptance, fresh => fresh.IsCancelRequested || fresh.IsEnded
                ? ApplicationImportVerifier.Ended(fresh, "The batch ended before it published.")
                : null,
            (ApplicationImportBatch fresh) => fresh.Commit(provenance, command.Intent, command.TargetProofManifest, plan.Rows.Content,
                plan.SourceRevision, now, operationResult), ct).ConfigureAwait(false);
    }

    public async ValueTask<Result<ReceiverOutcome>> FenceAsync(IExecutionContext context,
        FenceApplicationImportTarget command, CancellationToken ct)
    {
        if (Malformed(command.CommandId, command.OperationId, command.Generation))
            return SourceUseReceiverSupport.Permanent("The fence names another generation or operation.");
        var verified = await VerifyCancellationTargetAsync(command.Target, command.Source, command.BatchId,
            command.ApplicationId, command.Intent, ct).ConfigureAwait(false);
        if (verified.Failure is not null)
            return verified.Failure.Value;
        var (loaded, cancellation) = (verified.Context!, verified.Cancellation!);
        if (command.OperationId != cancellation.Header.RootOperationId)
            return SourceUseReceiverSupport.Permanent("The fence names another cancellation.");
        var batch = loaded.Batch;
        var provenance = ApplicationImportEffects.Child(command.CommandId, nameof(FenceApplicationImportTarget),
            command.Source.EventId, cancellation, command.Target, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        Result<ReceiverOutcome>? Early(DeclaredApplication application)
        {
            if (Decide(application.Acceptance, binding, application.Stream) is { } decided)
                return decided;
            if (application.FindImportHold(batch.Id) is { } hold)
            {
                if (hold.Fence is not null)
                    return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(application.Stream, hold.Fence));
                if (hold.Settled is not null)
                    return SourceUseReceiverSupport.Superseded("The target was already settled.",
                        NativeFactReference.Of(application.Stream, hold.Settled));
            }

            return null;
        }

        return await AppendAsync(context, () => new DeclaredApplication(loaded.TenantId, command.ApplicationId,
                command.CommandId), application => application.Acceptance, Early,
            (DeclaredApplication application) => application.Fence(provenance, command.Intent, batch.Id, command.ApplicationId), ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Result<ReceiverOutcome>> CompleteAsync(IExecutionContext context,
        CompleteApplicationImportCancellation command, CancellationToken ct)
    {
        if (Malformed(command.CommandId, command.OperationId, command.Generation))
            return SourceUseReceiverSupport.Permanent("The completion names another generation or operation.");
        var tenant = Uuid.Parse(command.Target.Realm, CultureInfo.InvariantCulture);
        var batch = await reader.HydrateAsync(new ApplicationImportBatch(tenant, command.BatchId, command.CommandId), ct)
            .ConfigureAwait(false);
        if (!batch.IsCreated || batch.CancellationAccepted is not { } accepted ||
            !ApplicationImportVerifier.IsFact(command.Source, batch, accepted))
            return SourceUseReceiverSupport.Transient("The accepted cancellation is not visible yet.");
        ImportFrozenPlanIntent cancellation;
        IReadOnlyList<ApplicationImportFenceProof> fences;
        try
        {
            cancellation = await ApplicationImportContent.ReadAsync<ImportFrozenPlanIntent>(content, tenant,
                command.Intent, ct).ConfigureAwait(false);
            fences = await ApplicationImportContent.ReadAsync<IReadOnlyList<ApplicationImportFenceProof>>(content,
                tenant, command.TargetFenceManifest, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                    $"The cancellation cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        if (command.Target != batch.Stream || command.Intent != accepted.Intent || command.BatchId != cancellation.BatchId ||
            command.OperationId != cancellation.Header.RootOperationId)
            return SourceUseReceiverSupport.Permanent("The completion differs from the accepted cancellation.");
        var provenance = ApplicationImportEffects.Child(command.CommandId, nameof(CompleteApplicationImportCancellation),
            command.Source.EventId, cancellation, batch.Stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        if (Decide(batch.Acceptance, binding, batch.Stream) is { } early)
            return early;
        if (batch.Committed is { } committed)
            return SourceUseReceiverSupport.Superseded(
                "The batch already published; the cancellation recovers forward.",
                NativeFactReference.Of(batch.Stream, committed));
        if (batch.Canceled is { } canceled)
            return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(batch.Stream, canceled));

        var expected = new List<Uuid>();
        if (batch.Plan is not null)
        {
            var sealedPlan = await verifier.LoadAsync(command.Target.Realm, command.BatchId, ct).ConfigureAwait(false);
            if (!sealedPlan.IsSuccess)
                return Result<ReceiverOutcome>.Failure(sealedPlan.Error!);
            var groups = await verifier.GroupsAsync(sealedPlan.Value!, ct).ConfigureAwait(false);
            if (!groups.IsSuccess)
                return Result<ReceiverOutcome>.Failure(groups.Error!);
            expected.AddRange(groups.Value!.Select(group => group.ApplicationId));
        }

        if (fences.Count != expected.Count || fences.Select(fence => fence.ApplicationId).Order()
                .SequenceEqual(expected.Order()) is false)
            return SourceUseReceiverSupport.Permanent("The fence manifest does not cover exactly the frozen targets.");
        foreach (var fence in fences)
        {
            var target = await reader.HydrateAsync(new DeclaredApplication(tenant, fence.ApplicationId), ct)
                .ConfigureAwait(false);
            if (target.FindImportHold(batch.Id)?.Fence is not { Metadata: { } metadata } held ||
                metadata.EventId != fence.EventId || metadata.AggregateVersion != fence.EventVersion)
                return SourceUseReceiverSupport.Transient("A target fence is not durable yet.");
            try
            {
                var actual = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, target, ct).ConfigureAwait(false);
                var stored = actual.SingleOrDefault(fact => fact.Metadata.EventId == fence.EventId);
                if (stored is null)
                    return SourceUseReceiverSupport.Transient("The exact target fence is not visible in its captured physical source prefix.");
                if (stored.GetType() != held.GetType() || stored.Metadata != held.Metadata ||
                    Kernel.ContentDigest.Sha256(stored, stored.GetType()) != Kernel.ContentDigest.Sha256(held, held.GetType()))
                    return SourceUseReceiverSupport.Permanent("The cancellation fence differs from its exact committed source payload and metadata.");
            }
            catch (ImmutableContentIntegrityException error)
            {
                return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                    $"The exact target fence cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
            }
        }

        var reason = cancellation.CancellationReason ?? string.Empty;
        var actor = cancellation.Header.OriginalActor;
        var actorMember = Uuid.TryParse(actor.Id, CultureInfo.InvariantCulture, out var memberId) ? memberId : Uuid.Empty;
        var now = clock.GetUtcNow();
        var operationResult = await ApplicationImportContent.UnitResultAsync(content, tenant, provenance.RootOperationId, ct)
            .ConfigureAwait(false);
        return await AppendAsync(context, () => new ApplicationImportBatch(tenant, batch.Id, command.CommandId),
            fresh => fresh.Acceptance, fresh => fresh.Committed is not null
                ? SourceUseReceiverSupport.Superseded("The batch already published; the cancellation recovers forward.",
                    NativeFactReference.Of(fresh.Stream, fresh.Committed))
                : null,
            (ApplicationImportBatch fresh) => fresh.CompleteCancellation(provenance, command.Intent, reason, actorMember, actor.Display, now, operationResult), ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Result<ReceiverOutcome>> ReleaseSourceAsync(IExecutionContext context,
        ReleaseApplicationImportSource command, CancellationToken ct)
    {
        if (Malformed(command.CommandId, command.OperationId, command.Generation))
            return SourceUseReceiverSupport.Permanent("The release names another generation or operation.");
        var verified = await VerifyTerminalAsync(command.Target.Realm, command.BatchId, command.Source, command.Intent,
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
            if (Decide(source.Acceptance, binding, source.Stream) is { } decided)
                return decided;
            return source.Find(batch.Id) is { Settled: { } settled }
                ? SourceUseReceiverSupport.Replayed(NativeFactReference.Of(source.Stream, settled))
                : null;
        }

        return await AppendAsync(context, () => new ApplicationImportSource(tenant, batch.SourceKey!,
                batch.SourceNamespace!, command.CommandId, batch.Id), source => source.Acceptance, Early,
            (ApplicationImportSource source) => source.Settle(provenance, command.Intent, batch.Id, disposition), ct).ConfigureAwait(false);
    }

    public async ValueTask<Result<ReceiverOutcome>> ReleaseTargetAsync(IExecutionContext context,
        ReleaseApplicationImportTarget command, CancellationToken ct)
    {
        if (Malformed(command.CommandId, command.OperationId, command.Generation))
            return SourceUseReceiverSupport.Permanent("The release names another generation or operation.");
        var verified = await VerifyTerminalAsync(command.Target.Realm, command.BatchId, command.Source, command.Intent,
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
        Result<ReceiverOutcome>? Early(DeclaredApplication application)
        {
            if (Decide(application.Acceptance, binding, application.Stream) is { } decided)
                return decided;
            var hold = application.FindImportHold(batch.Id);
            if (hold is null)
                return disposition == ApplicationImportDisposition.Published
                    ? SourceUseReceiverSupport.Permanent("A published batch has no hold on this target.")
                    : SourceUseReceiverSupport.Transient("The target fence is not visible yet.");
            if (hold.Settled is not null)
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(application.Stream, hold.Settled));
            if (disposition == ApplicationImportDisposition.Published && !hold.IsApplied)
                return SourceUseReceiverSupport.Transient("The published target effects are not visible yet.");
            return disposition != ApplicationImportDisposition.Published && hold.Fence is null
                ? SourceUseReceiverSupport.Transient("The target fence is not visible yet.")
                : null;
        }

        return await AppendAsync(context, () => new DeclaredApplication(tenant, command.ApplicationId,
                command.CommandId), application => application.Acceptance, Early,
            (DeclaredApplication application) => application.Settle(provenance, command.Intent, batch.Id, command.ApplicationId, disposition),
            ct).ConfigureAwait(false);
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

    sealed record TargetVerification(ApplicationImportContext? Context, ImportTargetGroupIntent? Group,
        Result<ReceiverOutcome>? Failure);

    sealed record CancellationVerification(ApplicationImportContext? Context, ImportFrozenPlanIntent? Cancellation,
        Result<ReceiverOutcome>? Failure);

    sealed record TerminalVerification(ApplicationImportBatch? Batch, DurableIntentHeader? Header,
        ApplicationImportDisposition Disposition, Result<ReceiverOutcome>? Failure);
}
