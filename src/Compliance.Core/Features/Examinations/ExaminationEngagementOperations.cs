using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Resolves native acceptance before mutable readiness/contact/preview preparation and freezes exact Unit results.</summary>
public sealed class ExaminationEngagementOperations(NativeOperationExecutor operations, ExaminationEngagementReader examinations,
    IImmutableContentStore content, TimeProvider clock)
{
    internal async ValueTask<Result> ExecuteAsync<TRequest>(IRequestContext<TRequest> context, Uuid tenant,
        Uuid program, Uuid examination, Uuid logical, string kind, long? expected,
        Func<ExaminationEngagement, Result> guard,
        Func<ExaminationEngagementReadSnapshot, OperationProvenance, CancellationToken,
            ValueTask<Result<ExaminationEngagementState>>> prepare, CancellationToken ct) where TRequest : IRequestBase
    {
        if (!ExaminationManagementActor.TryGet(context.Actor, context.Invocation, tenant, out var member, out var display))
            return Fail(RequestErrorKind.Forbidden, "An engagement decision requires its authenticated personal actor.");
        if (tenant == Uuid.Empty || program == Uuid.Empty || examination == Uuid.Empty || logical == Uuid.Empty || expected is < 0)
            return Fail(RequestErrorKind.Validation, "An engagement decision requires exact identities and a nonnegative original revision.");
        var stream = new ExaminationEngagement(tenant, program, examination).Stream;
        var operation = new OperationProvenance(logical, logical, null, kind,
            ActorReference.ForMember(member, display.Trim()), stream, RootOperation.InitialEpisode,
            expected is { } revision ? (ulong)revision : null, AuthoredIntentDigest.Compute(context.Request),
            clock.GetUtcNow(), stream, stream, stream);
        ExaminationEngagementState? state = null;
        ExaminationEngagementPreparedCut? cut = null;
        ImmutableContentRef? change = null;
        ImmutableContentRef? result = null;
        try
        {
            var outcome = await operations.ExecuteAsync(new NativeOperation<ExaminationEngagement>(AcceptedOperationBinding.Of(operation),
                () => new ExaminationEngagement(tenant, program, examination, logical, kind), owner => owner.Acceptance,
                async (owner, token) =>
                {
                    var valid = guard(owner);
                    if (!valid.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(valid.Error!);
                    var snapshot = await examinations.ReadAsync(tenant, program, examination, logical, kind, default, token)
                        .ConfigureAwait(false);
                    valid = guard(snapshot.Owner);
                    if (!valid.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(valid.Error!);
                    cut = ExaminationEngagementPreparedCut.Capture(snapshot.Owner);
                    var prepared = await prepare(snapshot, operation, token).ConfigureAwait(false);
                    if (!prepared.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                    state = prepared.Value!;
                    // An authored no-op still receives one durable logical acceptance and original result.
                    if (state.Engagement.Revision == snapshot.Owner.Revision && kind == nameof(ReviseExaminationEngagement))
                    {
                        var accepted = state.Engagement with
                        {
                            Revision = snapshot.Owner.Revision + 1,
                            UpdatedAt = operation.AcceptedAt,
                            ActorMemberId = member,
                            ActorDisplay = operation.Actor.Display
                        };
                        snapshot.Rules.Replay(new ExaminationEngagementRuleRevised(tenant, accepted, operation.AcceptedAt));
                        state = new ExaminationEngagementState(snapshot.Rules.Current!, null);
                    }
                    change = await content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(),
                        ExaminationEngagementState.Schema, state, token).ConfigureAwait(false);
                    var body = await content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(), UnitResult.Schema,
                        new UnitResult(), token).ConfigureAwait(false);
                    result = await content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(), "mutation-result-content.v1",
                        new MutationResultContent(UnitResult.Schema, new StandardResponseBody(body)), token).ConfigureAwait(false);
                    return Result<FirstResponseDescriptor>.Success(new FirstResponseDescriptor(200,
                        ImmutableContentStoreExtensions.JsonMediaType, UnitResult.Schema, new StandardResponseBody(body)));
                }, (owner, response) => guard(owner) is { IsSuccess: false } invalid ? invalid :
                    cut is null || !cut.Matches(owner) ? Fail(RequestErrorKind.Conflict,
                        "The exact engagement source changed during preparation; retry the decision.", true) :
                    owner.Accept(operation, state!, change!, response, result!)), context, ct).ConfigureAwait(false);
            return !outcome.IsSuccess ? Result.Failure(outcome.Error!) : outcome.Value is AcceptedAppendResult.Conflict
                ? Result.Failure(AcceptedAppendResult.ConflictError) : Result.Success;
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return Fail(RequestErrorKind.Conflict, "The engagement content is not available yet: " + exception.Message, true);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Fail(RequestErrorKind.Conflict, "The exact engagement content cannot be verified: " + exception.Message);
        }
    }
    internal static Result<ExaminationEngagementState> FromRules(ExaminationEngagementRules rules) =>
        Result<ExaminationEngagementState>.Success(new ExaminationEngagementState(rules.Current!, rules.Baseline));
    internal static Result<ExaminationEngagementState> Failure(CommandFailure failure) =>
        Result<ExaminationEngagementState>.Failure(failure.ToRequestError());
    static Result Fail(RequestErrorKind kind, string message, bool transient = false) =>
        Result.Failure(new RequestError(kind, message, transient));
}
