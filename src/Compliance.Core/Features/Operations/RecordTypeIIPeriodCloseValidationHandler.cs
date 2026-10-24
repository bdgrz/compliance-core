using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Native identified close reconciliation; exact retry replays before time, revision or live source checks.</summary>
public sealed class RecordTypeIIPeriodCloseValidationHandler(NativeOperationExecutor operations,
    ITypeIIPeriodCloseReconciliation sources, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<RecordTypeIIPeriodCloseValidation, UnitResult>
{
    public async ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<RecordTypeIIPeriodCloseValidation> context, CancellationToken ct)
    {
        var result = await HandleCoreAsync(context, ct).ConfigureAwait(false);
        return result.IsSuccess ? Result<UnitResult>.Success(new UnitResult()) : Result<UnitResult>.Failure(result.Error!);
    }

    async ValueTask<Result> HandleCoreAsync(IRequestContext<RecordTypeIIPeriodCloseValidation> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId, out var member, out var display) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Recording a period close requires a personal HTTP actor."));
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.PeriodId == Uuid.Empty || request.ExpectedRevision <= 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A close reconciliation requires exact identities and a positive expected revision."));
        var owner = new TypeIIPeriod(request.TenantId, request.ProgramId, request.PeriodId, request.CommandId);
        var operation = RootOperation.Provenance(request.CommandId, nameof(RecordTypeIIPeriodCloseValidation), owner.Stream,
            ActorReference.ForMember(member, display), request.ExpectedRevision, AuthoredIntentDigest.Compute(request), clock.GetUtcNow());
        TypeIIPeriodCloseValidationContent? reconciliation = null;
        ImmutableContentRef? change = null;
        ImmutableContentRef? result = null;
        try
        {
            var outcome = await operations.ExecuteAsync(new NativeOperation<TypeIIPeriod>(AcceptedOperationBinding.Of(operation),
                () => new TypeIIPeriod(request.TenantId, request.ProgramId, request.PeriodId, request.CommandId), period => period.Acceptance,
                async (period, token) =>
                {
                    if (period.Current is not { } current)
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound, "The observation period was not found."));
                    if (period.Revision != request.ExpectedRevision || period.PendingOperationId is not null ||
                        current.Status is not ("active" or "close_blocked" or "close_ready"))
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict, "The close reconciliation requires the current eligible period revision."));
                    if (DateOnly.FromDateTime(operation.AcceptedAt.UtcDateTime) <= current.PeriodEnd)
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict, "The complete observation interval must end before close reconciliation."));
                    try
                    {
                        var read = await sources.ReadAsync(period, context.Actor, user, operation, token).ConfigureAwait(false);
                        if (!read.IsSuccess)
                            return Result<FirstResponseDescriptor>.Failure(read.Error!);
                        reconciliation = read.Value;
                        if (reconciliation.RecordedThrough != operation.AcceptedAt || reconciliation.SourceContentManifest is null)
                            return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict, "The exact close receipt cutoff and published source proof are required."));
                        var proof = await SnapshotSourcePublicationReader.ReadAsync(content, request.TenantId, request.ProgramId,
                            current.ExaminationId, reconciliation.SourceContentManifest, token).ConfigureAwait(false);
                        if (reconciliation.SourceBlockers.Count == 0 && !TypeIBaselineSourceFamilies.Required.All(family =>
                                proof.SourceCoverage?.Count(row => row.Family == family) == 1))
                            return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                                "A ready close requires complete sealed source-family coverage, including every empty scan."));
                        if (proof.RecordedThrough != operation.AcceptedAt || proof.ClosePeriodId != request.PeriodId ||
                            proof.CloseOperationId != request.CommandId || reconciliation.SourceReferences.Any(reference =>
                                !proof.Sources.Any(source => source.SourceType == reference.SourceType && source.RecordId == reference.RecordId &&
                                    source.VersionId == reference.VersionId && source.Version == reference.Version && source.LogicalSha256 == reference.ContentSha256)))
                            return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                                "The complete close publication differs from its exact first owner, operation, cutoff or selected source versions."));
                        if (period.PrepareCloseValidation(request.ExpectedRevision, reconciliation, member, display, operation.AcceptedAt, out _) is { } failure)
                            return Result<FirstResponseDescriptor>.Failure(failure.ToRequestError());
                        change = await content.WriteJsonAsync(owner.Stream.Realm, Uuid.CreateVersion4(), TypeIIPeriodCloseValidationChange.Schema,
                            new TypeIIPeriodCloseValidationChange(new DurableIntentHeader(operation.RootOperationId, owner.Stream,
                                owner.Stream, owner.Stream, operation.Actor, operation.Episode, operation.OriginalExpectedRevision,
                                operation.AuthoredIntentSha256, operation.AcceptedAt), reconciliation), token).ConfigureAwait(false);
                        var frozen = await NativeContent.FreezeUnitAsync(content, request.TenantId, token).ConfigureAwait(false);
                        result = frozen.Result;
                        return Result<FirstResponseDescriptor>.Success(frozen.Response);
                    }
                    catch (ImmutableContentUnavailableException)
                    {
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict, "Exact close source bytes are not available yet.", true));
                    }
                    catch (ImmutableContentIntegrityException)
                    {
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict, "Exact close source content cannot be verified."));
                    }
                }, (period, response) => period.RecordCloseValidation(operation, reconciliation!, change!, response, result!) is { } invalid
                    ? Result.Failure(invalid.ToRequestError()) : Result.Success), context, ct).ConfigureAwait(false);
            return NativeContent.UnitOutcome(outcome);
        }
        catch (InvalidOperationException exception) when (exception.Message == "The Type II period event is invalid.")
        {
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The observation period was not found in this program."));
        }
        catch (ImmutableContentUnavailableException)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The original close response is not available yet.", true));
        }
        catch (ImmutableContentIntegrityException)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The original close response cannot be verified."));
        }
    }
}
