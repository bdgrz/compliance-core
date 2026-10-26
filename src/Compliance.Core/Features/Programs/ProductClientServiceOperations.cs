using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class ProductClientServiceOperations(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock)
{
    public const string RegistrationSchema = "client-service-registration.v1";

    public ValueTask<Result<ClientServiceRegistration>> CreateAsync(IRequestContext<CreateClientService> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.ProgramId == Uuid.Empty)
            return ValueTask.FromResult(Result<ClientServiceRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "A service requires its owning program.")));
        return ExecuteAsync<CreateClientService, ClientServiceRegistration>(context, request.ServiceId, nameof(CreateClientService), null,
            request with { Name = request.Name?.Trim()!, Purpose = request.Purpose?.Trim()!, OwnerReference = request.OwnerReference?.Trim()! }, ct);
    }

    public async ValueTask<Result> ReviseAsync(IRequestContext<ReviseClientService> context, CancellationToken ct)
    {
        var request = context.Request;
        var result = await ExecuteAsync<ReviseClientService, UnitResult>(context, request.CommandId, nameof(ReviseClientService),
            request.ExpectedRevision, request with
            {
                Name = request.Name?.Trim()!,
                Purpose = request.Purpose?.Trim()!,
                OwnerReference = request.OwnerReference?.Trim()!
            }, ct).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success : Result.Failure(result.Error!);
    }

    public async ValueTask<Result> RetireAsync(IRequestContext<RetireClientService> context, CancellationToken ct)
    {
        var request = context.Request;
        var result = await ExecuteAsync<RetireClientService, UnitResult>(context, request.CommandId, nameof(RetireClientService),
            request.ExpectedRevision, request with { Rationale = request.Rationale?.Trim()! }, ct).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success : Result.Failure(result.Error!);
    }

    async ValueTask<Result<TOut>> ExecuteAsync<TRequest, TOut>(IRequestContext<TRequest> context, Uuid logicalId, string kind,
        long? expectedRevision, object intent, CancellationToken ct) where TRequest : IRequestBase where TOut : notnull
    {
        var tenant = intent switch
        {
            CreateClientService value => value.TenantId,
            ReviseClientService value => value.TenantId,
            RetireClientService value => value.TenantId,
            _ => throw new InvalidOperationException("Another service command."),
        };
        var service = intent switch
        {
            CreateClientService value => value.ServiceId,
            ReviseClientService value => value.ServiceId,
            RetireClientService value => value.ServiceId,
            _ => throw new InvalidOperationException("Another service command."),
        };
        if (tenant == Uuid.Empty || service == Uuid.Empty || logicalId == Uuid.Empty)
            return Result<TOut>.Failure(new RequestError(RequestErrorKind.Validation, "A service command requires its authored identities."));
        var (member, display) = ClientServiceActor.Snapshot(context);
        if (RequestActor.IsSystem(context.Actor))
            throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var selected = new ClientService(tenant, service);
        var operation = OwnerProvenance.For(selected, logicalId, kind, ActorReference.ForMember(member, display), RootOperation.InitialEpisode,
            expectedRevision is { } expected ? unchecked((ulong)expected) : null, Digest(intent), clock.GetUtcNow()) with
        { FirstOwner = selected.Stream };
        ImmutableContentRef? preparedContent = null;
        ImmutableContentRef? frozenResult = null;
        PreparedCut? preparedCut = null;
        try
        {
            var accepted = await operations.ExecuteAsync(new NativeOperation<ClientService>(AcceptedOperationBinding.Of(operation),
                () => new ClientService(tenant, service, logicalId), owner => owner.Acceptance,
                async (owner, token) =>
                {
                    var ready = intent is CreateClientService ? owner.PrepareCreate() : owner.PrepareChange(expectedRevision!.Value);
                    if (!ready.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(ready.Error!);
                    if (intent is CreateClientService creation)
                    {
                        var program = await reader.HydrateAsync(new ComplianceProgram(tenant, creation.ProgramId), token).ConfigureAwait(false);
                        if (!program.IsCreated)
                            return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound, "The program was not found."));
                    }
                    if (intent is RetireClientService retirement)
                    {
                        if (string.IsNullOrWhiteSpace(retirement.Rationale))
                            return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Validation,
                                "Retiring a service requires a rationale."));
                        preparedContent = await NativeContent.WriteAsync(content, tenant, ProductClientServiceRetirement.Schema,
                            new ProductClientServiceRetirement(retirement.Rationale), token).ConfigureAwait(false);
                    }
                    else
                    {
                        var value = intent switch
                        {
                            CreateClientService create => new ProductClientServiceContent(create.Name, create.Purpose, create.OwnerReference),
                            ReviseClientService revise => new ProductClientServiceContent(revise.Name, revise.Purpose, revise.OwnerReference),
                            _ => throw new InvalidOperationException("Another service value kind."),
                        };
                        var failure = ProductClientServiceRules.Validate(value);
                        if (failure is not null)
                            return Result<FirstResponseDescriptor>.Failure(failure);
                        preparedContent = await NativeContent.WriteAsync(content, tenant, ProductClientServiceContent.Schema, value, token).ConfigureAwait(false);
                    }
                    var frozen = intent is CreateClientService
                        ? await NativeContent.FreezeAsync(content, tenant, RegistrationSchema, new ClientServiceRegistration(service), token).ConfigureAwait(false)
                        : await NativeContent.FreezeUnitAsync(content, tenant, token).ConfigureAwait(false);
                    frozenResult = frozen.Result;
                    preparedCut = PreparedCut.Of(owner);
                    return Result<FirstResponseDescriptor>.Success(frozen.Response);
                }, (owner, first) =>
                {
                    if (preparedCut != PreparedCut.Of(owner))
                        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The service source cut changed while its native operation was prepared.", true));
                    return intent switch
                    {
                        CreateClientService create => owner.Create(operation, create.ProgramId, preparedContent!, first, frozenResult!),
                        ReviseClientService => owner.Revise(operation, preparedContent!, first, frozenResult!),
                        RetireClientService => owner.Retire(operation, preparedContent!, first, frozenResult!),
                        _ => throw new InvalidOperationException("Another service command."),
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
            var schema = intent is CreateClientService ? RegistrationSchema : UnitResult.Schema;
            if (descriptor.HttpStatus != 200 || descriptor.ContentType != ImmutableContentStoreExtensions.JsonMediaType ||
                descriptor.ResultSchema != schema || descriptor.Body is not StandardResponseBody body || body.Content.Schema != schema)
                throw new ImmutableContentIntegrityException("The service original response has another status, schema or body kind.");
            var response = await content.ReadJsonAsync<TOut>(tenant.ToString(), body.Content, ct).ConfigureAwait(false);
            if (response is ClientServiceRegistration registration && registration.ServiceId != service)
                throw new ImmutableContentIntegrityException("The service original registration belongs to another exact creation identity.");
            return Result<TOut>.Success(response);
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result<TOut>.Failure(ProductClientServiceReadFailure.Of(error));
        }
    }

    static string Digest(object request) => request switch
    {
        CreateClientService value => AuthoredIntentDigest.Compute(value),
        ReviseClientService value => AuthoredIntentDigest.Compute(value),
        RetireClientService value => AuthoredIntentDigest.Compute(value),
        _ => throw new InvalidOperationException("Another service authored intent."),
    };
    sealed record PreparedCut(ulong Version, ulong Position, string PayloadDigest, string MetadataDigest)
    {
        public static PreparedCut Of(ClientService owner) => new(owner.Version, owner.CommittedStreamPosition,
            owner.NativeCut.Digest, owner.NativeCut.MetadataDigest);
    }

}
