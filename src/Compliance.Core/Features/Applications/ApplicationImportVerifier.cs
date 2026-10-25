using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     Verifies the proven source fact of an import receiver against the authoritative batch and reads its sealed
///     plan. A fact or content that is not visible yet is a transient failure; a fact that differs from the sealed
///     plan is a permanent conflict.
/// </summary>
sealed class ApplicationImportVerifier(IAggregateReader reader, IImmutableContentStore content)
{
    public async ValueTask<Result<ApplicationImportContext>> LoadAsync(string realm, Uuid batchId, CancellationToken ct)
    {
        var native = await LoadNativeAsync(realm, batchId, Uuid.Empty, ct).ConfigureAwait(false);
        return !native.IsSuccess ? Result<ApplicationImportContext>.Failure(native.Error!)
            : await PrepareAsync(native.Value!, ct).ConfigureAwait(false);
    }

    /// <summary>Reads the exact authoritative batch before immutable preparation or selected receiver decisions.</summary>
    public async ValueTask<Result<ApplicationImportNativeContext>> LoadNativeAsync(string realm, Uuid batchId,
        Uuid selectedLogicalId, CancellationToken ct)
    {
        var tenant = Uuid.Parse(realm, CultureInfo.InvariantCulture);
        var batch = await reader.HydrateAsync(new ApplicationImportBatch(tenant, batchId, selectedLogicalId), ct)
            .ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        return !batch.IsCreated || batch.Plan is null
            ? Result<ApplicationImportNativeContext>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The sealed import plan is not visible yet.", true))
            : Result<ApplicationImportNativeContext>.Success(new ApplicationImportNativeContext(tenant, batch));
    }

    /// <summary>Binds and validates the complete frozen plan for a new receiver candidate.</summary>
    public async ValueTask<Result<ApplicationImportContext>> PrepareAsync(ApplicationImportNativeContext native,
        CancellationToken ct)
    {
        var (tenant, batch) = (native.TenantId, native.Batch);
        var batchId = batch.Id;
        var accepted = batch.Plan!;
        ImportFrozenPlanIntent plan;
        try
        {
            plan = await ApplicationImportContent.ReadAsync<ImportFrozenPlanIntent>(content, tenant,
                accepted.FrozenPlan, ct).ConfigureAwait(false);
            if (plan.Header is null)
                throw new ImmutableContentIntegrityException("The frozen plan has no root header.");
            ApplicationImportIntentBindings.Require(plan.Header, accepted.Operation);
            if (plan.Rows.ItemSchema != "import-planned-row.v1" || plan.Rows.Order != SelectionOrder.Authored ||
                plan.TargetGroups.ItemSchema != ImportTargetGroupIntent.Schema || plan.TargetGroups.Order != SelectionOrder.CanonicalKey ||
                plan.SourceClaims.ItemSchema != ApplicationImportSourceClaimRecord.ItemSchema || plan.SourceClaims.Order != SelectionOrder.CanonicalKey ||
                plan.Rows.Count != accepted.RowCount || plan.TargetGroups.Count != accepted.TargetGroupCount ||
                accepted.PlanSha256 != accepted.FrozenPlan.Sha256)
                throw new ImmutableContentIntegrityException("The complete plan counts/digest differ from native admission.");
        }
        catch (ImmutableContentIntegrityException exception)
        {
            ct.ThrowIfCancellationRequested();
            return Result<ApplicationImportContext>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The frozen import plan cannot be verified yet: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        return plan.BatchId != batchId || plan.SourceKey != batch.SourceKey || plan.SourceNamespace != batch.SourceNamespace
            ? Result<ApplicationImportContext>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The frozen import plan differs from its batch.", false))
            : Result<ApplicationImportContext>.Success(new ApplicationImportContext(tenant, batch, plan));
    }

    /// <summary>The groups of the sealed plan.</summary>
    public async ValueTask<Result<IReadOnlyList<ImportTargetGroupIntent>>> GroupsAsync(
        ApplicationImportContext context, CancellationToken ct)
    {
        try
        {
            return Result<IReadOnlyList<ImportTargetGroupIntent>>.Success(
                await ApplicationImportContent.ReadSelectionAsync<ImportTargetGroupIntent>(content, context.TenantId,
                    context.Plan.TargetGroups, ct).ConfigureAwait(false));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            ct.ThrowIfCancellationRequested();
            return Result<IReadOnlyList<ImportTargetGroupIntent>>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The frozen target groups cannot be verified yet: {exception.Message}", exception is ImmutableContentUnavailableException));
        }
    }

    /// <summary>The rows of the sealed plan in original row order.</summary>
    public async ValueTask<Result<IReadOnlyList<ApplicationImportPlannedRow>>> RowsAsync(
        ApplicationImportContext context, CancellationToken ct)
    {
        try
        {
            return Result<IReadOnlyList<ApplicationImportPlannedRow>>.Success(
                await ApplicationImportContent.ReadSelectionAsync<ApplicationImportPlannedRow>(content,
                    context.TenantId, context.Plan.Rows, ct).ConfigureAwait(false));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            ct.ThrowIfCancellationRequested();
            return Result<IReadOnlyList<ApplicationImportPlannedRow>>.Failure(new RequestError(
                RequestErrorKind.Conflict, $"The frozen import rows cannot be verified yet: {exception.Message}", exception is ImmutableContentUnavailableException));
        }
    }

    /// <summary>True when the reference names the committed fact of the batch stream.</summary>
    public static bool IsFact(NativeFactReference source, ApplicationImportBatch batch, DomainEvent? fact) =>
        fact?.Metadata is { } metadata && source.Stream == batch.Stream && metadata.EventId == source.EventId &&
        metadata.AggregateVersion == source.AggregateVersion;

    /// <summary>The terminal fact of a batch and the disposition it requires of its holds.</summary>
    public static (DomainEvent Fact, ApplicationImportDisposition Disposition)? Terminal(ApplicationImportBatch batch) =>
        batch.Committed is { } committed ? (committed, ApplicationImportDisposition.Published)
        : batch.Canceled is { } canceled ? (canceled, ApplicationImportDisposition.Canceled)
        : batch.Failed is { } failed ? (failed, ApplicationImportDisposition.Failed)
        : null;

    /// <summary>The fact that ended or is ending the batch, offered as evidence of a supersession.</summary>
    public static NativeFactReference? Evidence(ApplicationImportBatch batch)
    {
        DomainEvent? fact = batch.Canceled;
        fact ??= batch.CancellationAccepted;
        fact ??= batch.Committed;
        fact ??= batch.Failed;
        return fact?.Metadata is null ? null : NativeFactReference.Of(batch.Stream, fact);
    }

    public static Result<ReceiverOutcome> Ended(ApplicationImportBatch batch, string reason) =>
        SourceUseReceiverSupport.Superseded(reason, Evidence(batch));

    public static NativeFactReference Reference(EventStreamAddress stream, DomainEvent fact) =>
        NativeFactReference.Of(stream, fact);
}

/// <summary>A verified batch with its sealed plan.</summary>
sealed record ApplicationImportContext(Uuid TenantId, ApplicationImportBatch Batch, ImportFrozenPlanIntent Plan)
{
    public ImmutableContentRef PlanRef => Batch.Plan!.FrozenPlan;
}
