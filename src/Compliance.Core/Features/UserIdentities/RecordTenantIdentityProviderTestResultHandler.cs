using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Records only results caused by the exact persisted provider-test request.</summary>
public sealed class RecordTenantIdentityProviderTestResultHandler(IAggregateExecutor executor,
    IDomainEventReader events, TimeProvider clock)
    : IRequestHandler<RecordTenantIdentityProviderTestResult>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<RecordTenantIdentityProviderTestResult> context, CancellationToken ct)
    {
        if (!RecordTenantIdentityProviderTestResultAuthorizer.IsNamedWorker(context))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the named identity-provider test worker may record provider results."));

        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProviderConnectionId == Uuid.Empty ||
            request.ExpectedRevision <= 0 || request.TestRequestEventId == Uuid.Empty ||
            request.Result is null || request.CompletedAt == default ||
            request.CompletedAt > clock.GetUtcNow())
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A provider-test result requires its exact request event and completion time."));
        if (context.CausationId != request.TestRequestEventId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The result must be caused by the exact provider-test request."));
        if (!await IsExactTestRequestAsync(request, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The request event does not match this exact provider-test attempt."));

        var actor = ActorReference.ForSystemProcess(
            RecordTenantIdentityProviderTestResultAuthorizer.ProcessId,
            "Tenant identity provider test worker");
        return await executor.ExecuteAsync(new TenantIdentityProvider(request.TenantId,
                request.ProviderConnectionId), provider =>
            {
                var current = provider.Current;
                if (current is null || current.TenantId != request.TenantId ||
                    current.ProviderConnectionId != request.ProviderConnectionId)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(
                        RequestErrorKind.NotFound, "The tenant identity provider was not found.")));
                if (current.TestHistory.Any(test =>
                        test.TestRequestEventId == request.TestRequestEventId))
                    return CommandFailureRequestAdapter.ToOutcome(provider.RecordTest(
                        request.ExpectedRevision, request.TestRequestEventId, request.Result,
                        actor, request.CompletedAt));
                if (provider.Revision != request.ExpectedRevision ||
                    current.PendingTest is not { } pending ||
                    pending.TestKey != request.Result.TestKey)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(
                        RequestErrorKind.Conflict,
                        "The result belongs to an earlier provider-test request or credential revision.")));

                return CommandFailureRequestAdapter.ToOutcome(provider.RecordTest(
                    request.ExpectedRevision, request.TestRequestEventId, request.Result,
                    actor, request.CompletedAt));
            }, context, ct).ConfigureAwait(false);
    }

    async ValueTask<bool> IsExactTestRequestAsync(RecordTenantIdentityProviderTestResult request,
        CancellationToken ct)
    {
        var stream = new TenantIdentityProvider(request.TenantId,
            request.ProviderConnectionId).Stream;
        await foreach (var record in events.ReadAsync(stream, 0, ct).WithCancellation(ct)
                           .ConfigureAwait(false))
        {
            if (record.Event.Metadata.EventId != request.TestRequestEventId)
                continue;
            return record.Stream == stream &&
                record.Event is TenantIdentityProviderTestRequested requested &&
                requested.TenantId == request.TenantId &&
                requested.SubjectId == request.ProviderConnectionId &&
                requested.Revision == request.ExpectedRevision &&
                requested.State is { } state && state.TenantId == request.TenantId &&
                state.ProviderConnectionId == request.ProviderConnectionId &&
                state.Revision == request.ExpectedRevision && state.Status != "disabled" &&
                requested.Request.TestKey == request.Result.TestKey &&
                requested.Request.CredentialRevision == state.CredentialRevision &&
                state.PendingTest == requested.Request &&
                request.CompletedAt >= requested.OccurredAt;
        }
        return false;
    }
}
