using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     Appends one service to the complete history of a client at the expected service epoch. The complete service
///     view is frozen as the first response, so an exact retry returns the original view even after later services.
/// </summary>
public sealed class RecordNonattestServiceHandler(NativeOperationExecutor operations, IImmutableContentStore content, TimeProvider clock)
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
            AuthoredIntentDigest.Compute(request), now, stream, stream, stream);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? serviceContent = null;
        ImmutableContentRef? result = null;
        (ulong Version, ulong Position, string Digest)? preparedCut = null;
        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(ClientServiceIndependence owner, CancellationToken token)
        {
            var ready = owner.PrepareService(request.ServiceRecordId, request.ExpectedServiceEpoch);
            if (!ready.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(ready.Error!);
            var view = IndependenceRecordValidation.Freeze(new NonattestServiceView(request.TenantId,
                request.ServiceRecordId, request.Content, actor, now));
            serviceContent = await IndependenceContent.WriteAsync(content, realm, IndependenceContent.ServiceSchema, view, token)
                .ConfigureAwait(false);
            result = await IndependenceContent.WriteResultAsync(content, realm, IndependenceContent.ServiceSchema,
                serviceContent, token).ConfigureAwait(false);
            preparedCut = (owner.Version, owner.CommittedStreamPosition, owner.ReplayCut.Digest);
            return Result<FirstResponseDescriptor>.Success(IndependenceContent.Ok(IndependenceContent.ServiceSchema, serviceContent));
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<ClientServiceIndependence>(binding,
            () => new ClientServiceIndependence(request.TenantId, request.ServiceRecordId), owner => owner.Acceptance,
            PrepareAsync,
            (ClientServiceIndependence owner, ImmutableContentRef frozen) =>
                preparedCut == (owner.Version, owner.CommittedStreamPosition, owner.ReplayCut.Digest)
                    ? owner.RecordService(provenance, serviceContent!, frozen, result!)
                    : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The prepared client service prefix changed before append.", true))), context, ct)
            .ConfigureAwait(false);
        return await IndependenceContent.ReadOutcomeAsync<NonattestServiceView>(content, realm, outcome, ct)
            .ConfigureAwait(false);
    }

}
