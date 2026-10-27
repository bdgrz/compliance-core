using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Authorizers;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
namespace Bdgrz.Compliance.Features.UserIdentities.Handlers;

/// <summary>
///     Records only results caused by the exact persisted provider-test request. The command id is the worker's
///     deterministic callback identity, so a redelivered result replays the original acceptance, and the aggregate
///     verifies the exact pending request from its own facts instead of scanning the stream.
/// </summary>
public sealed class RecordTenantIdentityProviderTestResultHandler(NativeOperationExecutor operations,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<RecordTenantIdentityProviderTestResult>
{
    const string CommandKind = nameof(RecordTenantIdentityProviderTestResult);

    /// <summary>The deterministic callback identity of the result of one persisted test request.</summary>
    public static Uuid CommandIdOf(Uuid testRequestEventId, Uuid tenantId, Uuid providerConnectionId) =>
        EffectIdentity.CommandId(testRequestEventId, "record_tenant_identity_provider_test_result",
            $"{tenantId}/{providerConnectionId}", null);

    public async ValueTask<Result> HandleAsync(IRequestContext<RecordTenantIdentityProviderTestResult> context,
        CancellationToken ct)
    {
        if (!RecordTenantIdentityProviderTestResultAuthorizer.IsNamedWorker(context))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the named identity-provider test worker may record provider results."));

        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty ||
            request.ProviderConnectionId == Uuid.Empty || request.ExpectedRevision <= 0 ||
            request.TestRequestEventId == Uuid.Empty || request.Result is null || request.CompletedAt == default ||
            request.CompletedAt > clock.GetUtcNow())
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A provider-test result requires its command, exact request event and completion time."));
        if (context.CausationId != request.TestRequestEventId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The result must be caused by the exact provider-test request."));

        var actor = ActorReference.ForSystemProcess(RecordTenantIdentityProviderTestResultAuthorizer.ProcessId,
            "Tenant identity provider test worker");
        var accepted = TenantIdentityProviderOperations.Provenance(request.TenantId, request.ProviderConnectionId,
            request.CommandId, CommandKind, actor, request.ExpectedRevision, AuthoredIntentDigest.Compute(request),
            clock.GetUtcNow());
        var provenance = accepted with { CausationEventId = request.TestRequestEventId };
        return await TenantIdentityProviderOperations.AdmitAsync(operations, content, context, provenance,
            request.TenantId, request.ProviderConnectionId,
            (TenantIdentityProvider provider) => provider.PrepareRecordTest(request.ExpectedRevision, request.TestRequestEventId, request.Result,
                actor, request.CompletedAt),
            (TenantIdentityProvider provider, ImmutableContentRef frozen) => provider.RecordTest(provenance, frozen, request.ExpectedRevision,
                request.TestRequestEventId, request.Result, actor, request.CompletedAt), ct).ConfigureAwait(false);
    }
}
