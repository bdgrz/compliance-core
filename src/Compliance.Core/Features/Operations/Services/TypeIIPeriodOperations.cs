using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Projections;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Operations.Services;

/// <summary>Native immediate Unit decisions; every source/preparation failure rechecks an equal concurrent acceptance.</summary>
public sealed class TypeIIPeriodOperations(NativeOperationExecutor operations, TypeIIPeriodReader periods,
    IImmutableContentStore content, TimeProvider clock)
{
    public async ValueTask<Result<UnitResult>> ExecuteAsync<TRequest>(IRequestContext<TRequest> context,
        Uuid tenantId, Uuid programId, Uuid periodId, Uuid commandId, string kind, long? expectedRevision,
        Func<TypeIIPeriodReadSnapshot, OperationProvenance, CancellationToken, ValueTask<Result>> prepare,
        CancellationToken ct) where TRequest : IRequestBase
    {
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, tenantId, out var member, out var display))
            return Fail(RequestErrorKind.Forbidden, "A period decision requires an authorized personal HTTP actor.");
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || periodId == Uuid.Empty || commandId == Uuid.Empty ||
            expectedRevision is <= 0)
            return Fail(RequestErrorKind.Validation, "A period decision requires exact identities and a positive original revision.");
        var stream = new TypeIIPeriod(tenantId, programId, periodId).Stream;
        var operation = RootOperation.Provenance(commandId, kind, stream, ActorReference.ForMember(member, display),
            expectedRevision, AuthoredIntentDigest.Compute(context.Request), clock.GetUtcNow());
        FrozenTypeIIPeriodMutation? frozen = null;
        ImmutableContentRef? result = null;
        ImmutableContentRef? calendarIntent = null;
        var sequence = 0L;
        var position = 0UL;
        try
        {
            var accepted = await operations.ExecuteAsync(new NativeOperation<TypeIIPeriod>(AcceptedOperationBinding.Of(operation),
                () => new TypeIIPeriod(tenantId, programId, periodId, commandId), owner => owner.Acceptance,
                async (owner, token) =>
                {
                    try
                    {
                        if (owner.Header is null || owner.ProgramId != programId)
                            return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound, "The observation period was not found."));
                        if (owner.PendingOperationId is not null || expectedRevision is { } expected && owner.Revision != expected)
                            return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict, "The period requires its exact current logical revision and no pending range change."));
                        var snapshot = await periods.ReadAsync(owner, token).ConfigureAwait(false);
                        var before = snapshot.View;
                        var ready = await prepare(snapshot, operation, token).ConfigureAwait(false);
                        if (!ready.IsSuccess)
                            return Result<FirstResponseDescriptor>.Failure(ready.Error!);
                        frozen = await TypeIIPeriodContent.FreezeAsync(content, before, snapshot.Rules, operation, token).ConfigureAwait(false);
                        var response = await NativeContent.FreezeUnitAsync(content, tenantId, token).ConfigureAwait(false);
                        result = response.Result;
                        calendarIntent = owner.CalendarIntent;
                        sequence = owner.NativeSequence;
                        position = owner.CommittedStreamPosition;
                        return Result<FirstResponseDescriptor>.Success(response.Response);
                    }
                    catch (ImmutableContentIntegrityException exception)
                    {
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The period's exact immutable preparation cannot be verified.", exception is ImmutableContentUnavailableException));
                    }
                }, (owner, response) => owner.Append(TypeIIPeriodContent.Fact(frozen!, operation, response, result!, sequence + 1,
                    calendarIntent), frozen!.Before, frozen.After, sequence, position)), context, ct).ConfigureAwait(false);
            if (!accepted.IsSuccess)
                return Result<UnitResult>.Failure(accepted.Error!);
            var descriptor = accepted.Value switch
            {
                AcceptedAppendResult.Committed committed => committed.Response,
                AcceptedAppendResult.Replayed replayed => replayed.Response,
                _ => null,
            };
            if (descriptor is null)
                return Result<UnitResult>.Failure(AcceptedAppendResult.ConflictError);
            if (descriptor.HttpStatus != 200 || descriptor.ResultSchema != UnitResult.Schema ||
                descriptor.Body is not StandardResponseBody body || body.Content.Schema != UnitResult.Schema)
                throw new ImmutableContentIntegrityException("The original period decision has another response contract.");
            return await NativeContent.ResponseAsync<UnitResult>(content, tenantId, accepted, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The period's exact immutable content cannot be verified.", exception is ImmutableContentUnavailableException));
        }
    }

    public static Result Outcome(Bdgrz.Compliance.Features.Versioning.Models.CommandFailure? failure) =>
        failure is null ? Result.Success : Result.Failure(failure.ToRequestError());
    static Result<UnitResult> Fail(RequestErrorKind kind, string message) => Result<UnitResult>.Failure(new RequestError(kind, message));
}
