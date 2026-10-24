using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     Records a report as the first fact of its own owner. The report id is the logical identity: an exact retry
///     replays the original result even after a later revision, provider change or artifact change, and a changed
///     actor or intent under the same id conflicts. Provider and artifact are observed only for a new operation.
/// </summary>
public sealed class RecordAssuranceReportHandler(NativeOperationExecutor operations, IAggregateReader reader,
    AssuranceReferences references, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<RecordAssuranceReport, AssuranceReportRegistration>
{
    const string CommandKind = nameof(RecordAssuranceReport);
    const string Episode = "initial";

    public async ValueTask<Result<AssuranceReportRegistration>> HandleAsync(
        IRequestContext<RecordAssuranceReport> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.ReportId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProviderId == Uuid.Empty)
            return Fail(RequestErrorKind.Validation, "An assurance report requires nonempty identities.");
        if (AssuranceRules.InputError(request.Content) is { } inputError)
            return Fail(RequestErrorKind.Validation, inputError);
        var normalized = AssuranceRules.Normalize(request.Content);
        if (AssuranceRules.Validate(normalized) is { } error)
            return Fail(RequestErrorKind.Validation, error);
        var actor = ProviderActor.From(context);
        var now = clock.GetUtcNow();
        var provenance = Owner(request).Provenance(request.ReportId, CommandKind, actor, Episode, null,
            AuthoredIntentDigest.Compute(request with { Content = normalized }), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        var prior = await reader.HydrateAsync(Owner(request), ct).ConfigureAwait(false);
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? contentRef = null;
        ImmutableContentRef? resultRef = null;
        var providerRevision = 0L;
        if (prior.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = prior.PrepareRecord();
            if (!prepared.IsSuccess)
                return Result<AssuranceReportRegistration>.Failure(prepared.Error!);
            var provider = await references.ProviderAsync(request.TenantId, request.ProviderId, ct)
                .ConfigureAwait(false);
            if (!provider.IsSuccess)
                return Result<AssuranceReportRegistration>.Failure(provider.Error);
            var artifact = await references.ArtifactAsync(request.TenantId, normalized.Citation, ct)
                .ConfigureAwait(false);
            if (!artifact.IsSuccess)
                return Result<AssuranceReportRegistration>.Failure(artifact.Error);
            providerRevision = provider.Value.Revision;
            contentRef = await ProviderAssuranceContent.WriteAsync(content, request.TenantId,
                ProviderAssuranceContent.ReportRecordedSchema, normalized, ct).ConfigureAwait(false);
            resultRef = await ProviderAssuranceContent.WriteAsync(content, request.TenantId,
                ProviderAssuranceContent.ReportRegistrationSchema,
                new AssuranceReportRegistration(request.ReportId, 1), ct).ConfigureAwait(false);
            response = ProviderAssuranceContent.Completed(resultRef, ProviderAssuranceContent.ReportRegistrationSchema);
        }
        var accepted = await operations.ExecuteAsync(new NativeOperation<ProviderAssuranceReport>(binding,
            () => Owner(request), owner => owner.Acceptance,
            owner =>
            {
                var checkedRecord = owner.PrepareRecord();
                return checkedRecord.IsSuccess && response is not null
                    ? Result<FirstResponseDescriptor>.Success(response)
                    : Result<FirstResponseDescriptor>.Failure(checkedRecord.IsSuccess
                        ? AcceptedAppendResult.ConflictError
                        : checkedRecord.Error!);
            },
            (ProviderAssuranceReport owner, ImmutableContentRef frozen) => owner.Record(provenance, contentRef!,
                providerRevision, actor, now, frozen, resultRef!)), context, ct).ConfigureAwait(false);
        return await ProviderAssuranceContent.ReadFirstResponseAsync<AssuranceReportRegistration>(content,
            request.TenantId, ProviderAssuranceContent.ReportRegistrationSchema, accepted, ct).ConfigureAwait(false);
    }

    static ProviderAssuranceReport Owner(RecordAssuranceReport request) =>
        new(request.TenantId, request.ProviderId, request.ReportId, request.ReportId);

    static Result<AssuranceReportRegistration> Fail(RequestErrorKind kind, string message) =>
        Result<AssuranceReportRegistration>.Failure(new RequestError(kind, message));
}
