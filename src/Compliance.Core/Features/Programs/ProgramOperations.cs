using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class ProgramOperations(NativeOperationExecutor operations, IImmutableContentStore content, TimeProvider clock)
{
    public const string RegistrationSchema = "program-registration.v1";

    public ValueTask<Result<ProgramRegistration>> CreateAsync(IRequestContext<CreateProgram> context, CancellationToken ct) =>
        ExecuteAsync<CreateProgram, ProgramRegistration>(context, context.Request.ProgramId, nameof(CreateProgram), null,
            context.Request with { Name = context.Request.Name?.Trim()! }, ct);

    public async ValueTask<Result> ReviseAsync(IRequestContext<ReviseProgram> context, CancellationToken ct)
    {
        var request = context.Request;
        var result = await ExecuteAsync<ReviseProgram, UnitResult>(context, request.CommandId, nameof(ReviseProgram), request.ExpectedRevision,
            request with { Name = request.Name?.Trim()! }, ct).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success : Result.Failure(result.Error!);
    }

    public async ValueTask<Result> SelectAsync(IRequestContext<SelectProgramCriteriaEdition> context, CancellationToken ct)
    {
        var request = context.Request;
        var result = await ExecuteAsync<SelectProgramCriteriaEdition, UnitResult>(context, request.CommandId,
            nameof(SelectProgramCriteriaEdition), request.ExpectedRevision, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success : Result.Failure(result.Error!);
    }

    async ValueTask<Result<TOut>> ExecuteAsync<TRequest, TOut>(IRequestContext<TRequest> context, Uuid logicalId,
        string kind, long? expectedRevision, object intent, CancellationToken ct)
        where TRequest : IRequestBase where TOut : notnull
    {
        var tenant = intent switch
        {
            CreateProgram value => value.TenantId,
            ReviseProgram value => value.TenantId,
            SelectProgramCriteriaEdition value => value.TenantId,
            _ => throw new InvalidOperationException("Another program command.")
        };
        var program = intent switch
        {
            CreateProgram value => value.ProgramId,
            ReviseProgram value => value.ProgramId,
            SelectProgramCriteriaEdition value => value.ProgramId,
            _ => throw new InvalidOperationException("Another program command.")
        };
        if (tenant == Uuid.Empty || program == Uuid.Empty || logicalId == Uuid.Empty)
            return Result<TOut>.Failure(new RequestError(RequestErrorKind.Validation, "A program command requires its authored identities."));
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId) || RequestActor.IsSystem(context.Actor))
            throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var owner = new ComplianceProgram(tenant, program);
        var operation = OwnerProvenance.For(owner, logicalId, kind,
            ActorReference.ForMember(RbacIds.Member(tenant, userId), UserIdentityClaims.BdgrzDisplay(context.Actor, userId)),
            RootOperation.InitialEpisode, expectedRevision is { } expected ? unchecked((ulong)expected) : null,
            IntentDigest(intent), clock.GetUtcNow()) with
        { FirstOwner = owner.Stream };
        NativePreparedCut? preparedCut = null;
        ImmutableContentRef? authored = null;
        ImmutableContentRef? result = null;
        try
        {
            var accepted = await operations.ExecuteAsync(new NativeOperation<ComplianceProgram>(AcceptedOperationBinding.Of(operation),
                () => new ComplianceProgram(tenant, program, logicalId), current => current.Acceptance,
                async (current, token) =>
                {
                    preparedCut = NativePreparedCut.Of(current, current.NativeCut.Digest, current.NativeCut.MetadataDigest);
                    Result ready;
                    ProgramContent? value = null;
                    if (intent is CreateProgram create)
                    {
                        ready = current.PrepareCreate();
                        value = new ProgramContent(create.Name, create.Plan);
                    }
                    else if (intent is ReviseProgram revise)
                    {
                        ready = current.PrepareRevise(revise.ExpectedRevision);
                        value = new ProgramContent(revise.Name, revise.Plan);
                    }
                    else
                    {
                        var selection = (SelectProgramCriteriaEdition)intent;
                        var decision = current.PrepareSelection(selection.ExpectedRevision, selection.EditionId);
                        ready = decision.IsSuccess ? Result.Success : Result.Failure(decision.Error!);
                    }
                    if (!ready.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(ready.Error!);
                    if (value is not null)
                    {
                        var failure = ProgramRules.Validate(value);
                        if (failure is not null)
                            return Result<FirstResponseDescriptor>.Failure(failure.ToRequestError());
                        authored = await NativeContent.WriteAsync(content, tenant, ProgramContent.Schema, value, token).ConfigureAwait(false);
                    }
                    var frozen = intent is CreateProgram
                        ? await NativeContent.FreezeAsync(content, tenant, RegistrationSchema, new ProgramRegistration(program), token).ConfigureAwait(false)
                        : await NativeContent.FreezeUnitAsync(content, tenant, token).ConfigureAwait(false);
                    result = frozen.Result;
                    return Result<FirstResponseDescriptor>.Success(frozen.Response);
                }, (current, first) =>
                {
                    if (preparedCut is null || preparedCut != NativePreparedCut.Of(current, current.NativeCut.Digest, current.NativeCut.MetadataDigest))
                        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The exact program prefix changed during preparation; reload and retry.", true));
                    return intent switch
                    {
                        CreateProgram => current.Create(operation, authored!, first, result!),
                        ReviseProgram => current.Revise(operation, authored!, first, result!),
                        SelectProgramCriteriaEdition selection => current.SelectCriteriaEdition(operation, selection.EditionId, first, result!),
                        _ => throw new InvalidOperationException("Another program command."),
                    };
                }), context, ct).ConfigureAwait(false);
            if (!accepted.IsSuccess)
                return Result<TOut>.Failure(accepted.Error!);
            var descriptor = accepted.Value switch
            {
                AcceptedAppendResult.Committed committed => committed.Response,
                AcceptedAppendResult.Replayed replayed => replayed.Response,
                _ => null,
            };
            if (descriptor is null)
                return Result<TOut>.Failure(AcceptedAppendResult.ConflictError);
            var schema = intent is CreateProgram ? RegistrationSchema : UnitResult.Schema;
            if (descriptor.HttpStatus != 200 || descriptor.ContentType != ImmutableContentStoreExtensions.JsonMediaType ||
                descriptor.ResultSchema != schema || descriptor.Body is not StandardResponseBody body || body.Content.Schema != schema)
                throw new ImmutableContentIntegrityException("The program original response has another status, schema or body kind.");
            var response = await content.ReadJsonAsync<TOut>(tenant.ToString(), body.Content, ct).ConfigureAwait(false);
            if (response is ProgramRegistration registration && registration.ProgramId != program)
                throw new ImmutableContentIntegrityException("The program original registration has another exact creation identity.");
            return Result<TOut>.Success(response);
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result<TOut>.Failure(ProgramReadFailure.Of(error));
        }
    }
    static string IntentDigest(object intent) => intent switch
    {
        CreateProgram value => AuthoredIntentDigest.Compute(value),
        ReviseProgram value => AuthoredIntentDigest.Compute(value),
        SelectProgramCriteriaEdition value => AuthoredIntentDigest.Compute(value),
        _ => throw new InvalidOperationException("Another program authored intent."),
    };

}
