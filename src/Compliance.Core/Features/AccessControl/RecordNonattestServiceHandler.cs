using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Appends one service to the complete history of a client at the expected service epoch. The complete service
///     view is frozen as the first response, so an exact retry returns the original view even after later services.
/// </summary>
public sealed class RecordNonattestServiceHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<RecordNonattestService, NonattestServiceView>
{
    const string CommandKind = "RecordNonattestService";
    const string InitialEpisode = "initial";

    public async ValueTask<Result<NonattestServiceView>> HandleAsync(IRequestContext<RecordNonattestService> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ServiceRecordId == Uuid.Empty ||
            !IndependenceRecordValidation.ValidService(request.Content))
            return IndependenceContent.Failure<NonattestServiceView>(RequestErrorKind.Validation,
                "A service requires an owning client, immutable identity, valid period, staff and attributed source.");
        if (request.ExpectedServiceEpoch < 0)
            return IndependenceContent.Failure<NonattestServiceView>(RequestErrorKind.Validation,
                "The expected service epoch cannot be negative.");

        var now = clock.GetUtcNow();
        var actor = AccessGrantActor.From(context, request.TenantId);
        var realm = IndependenceContent.Realm(request.TenantId);
        var stream = new ClientServiceIndependence(request.TenantId).Stream;
        var provenance = new OperationProvenance(request.ServiceRecordId, request.ServiceRecordId, null, CommandKind,
            actor, stream, InitialEpisode, (ulong)request.ExpectedServiceEpoch,
            AuthoredIntentDigest.Compute(request), now, stream, stream);
        var binding = AcceptedOperationBinding.Of(provenance);

        // The view is derived from the authored service only, so it is built before the append; the owner guard
        // re-checks the epoch on every attempt.
        var current = await reader.HydrateAsync(
            new ClientServiceIndependence(request.TenantId, request.ServiceRecordId), ct).ConfigureAwait(false);
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? serviceContent = null;
        ImmutableContentRef? result = null;
        if (current.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var view = IndependenceRecordValidation.Freeze(new NonattestServiceView(request.TenantId,
                request.ServiceRecordId, request.Content, actor, now));
            serviceContent = await IndependenceContent.WriteAsync(content, realm, IndependenceContent.ServiceSchema,
                view, ct).ConfigureAwait(false);
            result = await IndependenceContent.WriteResultAsync(content, realm, IndependenceContent.ServiceSchema,
                serviceContent, ct).ConfigureAwait(false);
            response = IndependenceContent.Ok(IndependenceContent.ServiceSchema, serviceContent);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<ClientServiceIndependence>(binding,
            () => new ClientServiceIndependence(request.TenantId, request.ServiceRecordId), owner => owner.Acceptance,
            owner => Prepare(owner, request, response),
            (ClientServiceIndependence owner, ImmutableContentRef frozen) =>
                owner.RecordService(provenance, serviceContent!, frozen, result!)), context, ct)
            .ConfigureAwait(false);
        return await IndependenceContent.ReadOutcomeAsync<NonattestServiceView>(content, realm, outcome, ct)
            .ConfigureAwait(false);
    }

    static Result<FirstResponseDescriptor> Prepare(ClientServiceIndependence owner, RecordNonattestService request,
        FirstResponseDescriptor? response)
    {
        var prepared = owner.PrepareService(request.ServiceRecordId, request.ExpectedServiceEpoch);
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return response is null
            ? IndependenceContent.Failure<FirstResponseDescriptor>(RequestErrorKind.Conflict,
                "The service history changed while the service was prepared; reload and retry.", true)
            : Result<FirstResponseDescriptor>.Success(response);
    }
}
