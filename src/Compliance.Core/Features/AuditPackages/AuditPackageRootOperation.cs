using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Resolves exact acceptance before source/currentness checks, then freezes and appends one owner delta.</summary>
public sealed class AuditPackageRootOperation(IAggregateReader reader, AuditPackageReader packages,
    NativeOperationExecutor operations, IImmutableContentStore content, TimeProvider clock)
{
    internal async ValueTask<Result<AcceptedAppendResult>> ExecuteAsync<TRequest>(IRequestContext<TRequest> context,
        Uuid tenant, Uuid program, Uuid packageId, Uuid logicalId, string kind, long? expectedRevision,
        Func<AuditPackageManifest, long> newVersion, Func<AuditPackageManifest, Result> guard,
        Func<AuditPackageSnapshot, OperationProvenance, CancellationToken, ValueTask<Result<PreparedPackageDelta>>> prepare,
        Func<AuditPackageManifest, OperationProvenance, PreparedPackageDelta, ImmutableContentRef,
            ImmutableContentRef, Result> apply, bool accepted, CancellationToken ct) where TRequest : IRequestBase
    {
        if (!AuditPackageCommandActor.TryGet(context.Actor, context.Invocation, tenant, out var member, out var display))
            return Fail(RequestErrorKind.Forbidden, "A package decision requires a personal HTTP actor.");
        if (tenant == Uuid.Empty || program == Uuid.Empty || packageId == Uuid.Empty || logicalId == Uuid.Empty ||
            expectedRevision is < 0)
            return Fail(RequestErrorKind.Validation, "A package command requires exact identities and a nonnegative revision.");
        try
        {
            var owner = await reader.HydrateAsync(new AuditPackageManifest(tenant, packageId, logicalId, kind), ct)
                .ConfigureAwait(false);
            if (owner.IsCreated && owner.ProgramId != program)
                return Fail(RequestErrorKind.NotFound, "The package was not found in this program.");
            var version = owner.Acceptance.SelectedFact is IAuditPackageManifestFact existing
                ? existing.PackageVersion : newVersion(owner);
            var now = clock.GetUtcNow();
            var operation = new OperationProvenance(logicalId, logicalId, null, kind,
                ActorReference.ForMember(member, display), owner.Stream, version.ToString(CultureInfo.InvariantCulture),
                expectedRevision is { } expected ? (ulong)expected : null, AuthoredIntentDigest.Compute(context.Request),
                now, owner.Stream, owner.Stream, owner.Stream);
            var binding = AcceptedOperationBinding.Of(operation);
            PreparedPackageDelta? delta = null;
            AuditPackagePreparedCut? cut = null;
            ImmutableContentRef? result = null;
            return await operations.ExecuteAsync(new NativeOperation<AuditPackageManifest>(binding,
                () => new AuditPackageManifest(tenant, packageId, logicalId, kind, version), aggregate => aggregate.Acceptance,
                async (aggregate, token) =>
                {
                    try
                    {
                        var valid = guard(aggregate);
                        if (!valid.IsSuccess)
                            return Result<FirstResponseDescriptor>.Failure(valid.Error!);
                        var snapshot = await packages.ReadAsync(tenant, program, packageId, logicalId, kind, version, token)
                            .ConfigureAwait(false);
                        valid = guard(snapshot.Owner);
                        if (!valid.IsSuccess)
                            return Result<FirstResponseDescriptor>.Failure(valid.Error!);
                        cut = AuditPackagePreparedCut.Capture(snapshot.Owner);
                        var prepared = await prepare(snapshot, operation, token).ConfigureAwait(false);
                        if (!prepared.IsSuccess)
                            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                        var body = accepted
                            ? await PackageContent.WriteAsync(content, tenant, "operation-accepted-response.v1",
                                new OperationAcceptedResponse(logicalId,
                                    $"/api/v1/tenants/{tenant}/programs/{program}/operations/{logicalId}", now), token)
                                .ConfigureAwait(false)
                            : await PackageContent.WriteAsync(content, tenant, UnitResult.Schema, new UnitResult(), token)
                                .ConfigureAwait(false);
                        var schema = accepted ? "operation-accepted-response.v1" : UnitResult.Schema;
                        var response = new FirstResponseDescriptor(accepted ? 202 : 200,
                            ImmutableContentStoreExtensions.JsonMediaType, schema, new StandardResponseBody(body));
                        var mutationResult = await PackageContent.WriteAsync(content, tenant, "mutation-result-content.v1",
                            new MutationResultContent(schema, new StandardResponseBody(body)), token).ConfigureAwait(false);
                        delta = prepared.Value;
                        result = mutationResult;
                        return Result<FirstResponseDescriptor>.Success(response);
                    }
                    catch (ImmutableContentUnavailableException exception)
                    {
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                            "Package content is not available yet: " + exception.Message, true));
                    }
                    catch (ImmutableContentIntegrityException exception)
                    {
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                            "Package content cannot be verified: " + exception.Message, false));
                    }
                },
                (aggregate, frozen) => guard(aggregate) is { IsSuccess: false } invalid
                    ? invalid : cut is null || !cut.Matches(aggregate)
                    ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The exact package source changed while its candidate was prepared; retry the operation.", true))
                    : apply(aggregate, operation, delta!, frozen, result!)), context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return Fail(RequestErrorKind.Conflict, "Package content is not available yet: " + exception.Message, true);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Fail(RequestErrorKind.Conflict, "Package content cannot be verified: " + exception.Message, false);
        }
    }

    internal static Result Unit(Result<AcceptedAppendResult> outcome) => !outcome.IsSuccess
        ? Result.Failure(outcome.Error!) : outcome.Value is AcceptedAppendResult.Conflict
            ? Result.Failure(AcceptedAppendResult.ConflictError) : Result.Success;

    internal async ValueTask<Result<OperationAcceptedResponse>> AcknowledgmentAsync(Uuid tenant,
        Result<AcceptedAppendResult> outcome, CancellationToken ct)
    {
        if (!outcome.IsSuccess)
            return Result<OperationAcceptedResponse>.Failure(outcome.Error!);
        var response = outcome.Value switch
        {
            AcceptedAppendResult.Committed committed => committed.Response,
            AcceptedAppendResult.Replayed replayed => replayed.Response,
            _ => null
        };
        if (response?.Body is not StandardResponseBody body)
            return Result<OperationAcceptedResponse>.Failure(AcceptedAppendResult.ConflictError);
        return Result<OperationAcceptedResponse>.Success(await PackageContent.ReadAsync<OperationAcceptedResponse>(content,
            tenant, body.Content, ct).ConfigureAwait(false));
    }

    static Result<AcceptedAppendResult> Fail(RequestErrorKind kind, string message, bool transient = false) =>
        Result<AcceptedAppendResult>.Failure(new RequestError(kind, message, transient));
}
