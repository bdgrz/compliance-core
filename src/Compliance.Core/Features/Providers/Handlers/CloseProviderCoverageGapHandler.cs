using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Providers.Handlers;

/// <summary>
///     Closes a provider gap once, only against a verified newer or different source. The command id is the logical
///     identity: an exact retry replays the original result even after later facts, and a changed actor, intent or
///     expected revision under the same id conflicts. A closure never erases an already admitted risk link.
/// </summary>
public sealed class CloseProviderCoverageGapHandler(NativeOperationExecutor operations, AssuranceReferences references, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<CloseProviderCoverageGap, ProviderCoverageGapRegistration>
{
    const string CommandKind = nameof(CloseProviderCoverageGap);
    const string Episode = "current";

    public ValueTask<Result<ProviderCoverageGapRegistration>> HandleAsync(
        IRequestContext<CloseProviderCoverageGap> context, CancellationToken ct) =>
        ProviderGapFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result<ProviderCoverageGapRegistration>> HandleCoreAsync(
        IRequestContext<CloseProviderCoverageGap> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProviderId == Uuid.Empty ||
            request.GapId == Uuid.Empty || request.ExpectedRevision < 1)
            return Fail(RequestErrorKind.Validation,
                "A provider coverage gap closure requires nonempty identities and the expected revision.");
        if (ProviderCoverageGapRules.ClosureInputError(request.Content) is { } inputError)
            return Fail(RequestErrorKind.Validation, inputError);
        var normalized = ProviderCoverageGapRules.Normalize(request.Content);
        var actor = ProviderActor.From(context);
        var now = clock.GetUtcNow();
        var provenance = Owner(request).Provenance(request.CommandId, CommandKind, actor, Episode,
            (ulong)request.ExpectedRevision, AuthoredIntentDigest.Compute(request with { Content = normalized }), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        ImmutableContentRef? contentRef = null;
        ImmutableContentRef? resultRef = null;
        ProviderCoverageGapContent? recorded = null;
        ProviderGapPreparedCut? preparedCut = null;
        var accepted = await operations.ExecuteAsync(new NativeOperation<ProviderAssuranceGap>(binding,
            () => Owner(request), owner => owner.Acceptance,
            async (owner, token) =>
            {
                preparedCut = ProviderGapPreparedCut.Capture(owner);
                var provider = await references.ProviderAsync(request.TenantId, request.ProviderId, token)
                    .ConfigureAwait(false);
                if (!provider.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(provider.Error);
                if (!owner.Exists)
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound, "The provider coverage gap was not found."));
                try
                {
                    recorded = await ProviderAssuranceContent.ReadAsync<ProviderCoverageGapContent>(content,
                        request.TenantId, owner.Recorded!.ContentContent, token).ConfigureAwait(false);
                }
                catch (ImmutableContentIntegrityException exception)
                {
                    token.ThrowIfCancellationRequested();
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                        $"The recorded provider coverage gap cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
                }
                var prepared = owner.PrepareClose(request.ExpectedRevision, recorded, normalized);
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                var source = await references.CoverageGapSourceAsync(request.TenantId, provider.Value, recorded.ServiceId,
                    normalized.SourceKind, normalized.SourceId, normalized.SourceRevision, token).ConfigureAwait(false);
                if (!source.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(source.Error);
                contentRef = await ProviderAssuranceContent.WriteAsync(content, request.TenantId,
                    ProviderAssuranceContent.GapClosedSchema, normalized, token).ConfigureAwait(false);
                resultRef = await ProviderAssuranceContent.WriteAsync(content, request.TenantId,
                    ProviderAssuranceContent.GapRegistrationSchema,
                    new ProviderCoverageGapRegistration(request.GapId, request.ExpectedRevision + 1), token)
                    .ConfigureAwait(false);
                return Result<FirstResponseDescriptor>.Success(ProviderAssuranceContent.Completed(resultRef, ProviderAssuranceContent.GapRegistrationSchema));
            },
            (ProviderAssuranceGap owner, ImmutableContentRef frozen) =>
            {
                var guarded = owner.PrepareClose(request.ExpectedRevision, recorded!, normalized);
                if (!guarded.IsSuccess)
                    return guarded;
                return preparedCut is null || !preparedCut.Matches(owner) ? ProviderGapPreparedCut.Changed()
                    : owner.Close(provenance, request.ExpectedRevision, recorded!, normalized, contentRef!, actor, now,
                        frozen, resultRef!);
            }), context, ct).ConfigureAwait(false);
        return await ProviderAssuranceContent.ReadFirstResponseAsync<ProviderCoverageGapRegistration>(content,
            request.TenantId, ProviderAssuranceContent.GapRegistrationSchema, accepted, ct).ConfigureAwait(false);
    }

    static ProviderAssuranceGap Owner(CloseProviderCoverageGap request) =>
        new(request.TenantId, request.ProviderId, request.GapId, request.CommandId);

    static Result<ProviderCoverageGapRegistration> Fail(RequestErrorKind kind, string message) =>
        Result<ProviderCoverageGapRegistration>.Failure(new RequestError(kind, message));
}
