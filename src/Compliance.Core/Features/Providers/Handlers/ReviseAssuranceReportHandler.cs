using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
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
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Providers.Handlers;

/// <summary>
///     Appends a revision at the expected revision of the report owner. The command id is the logical identity: an
///     exact retry replays the original result even after a later revision, and a changed actor, intent or expected
///     revision under the same id conflicts. A report held by a provider review refuses the revision until it settles.
/// </summary>
public sealed class ReviseAssuranceReportHandler(NativeOperationExecutor operations, IAggregateReader reader,
    AssuranceReferences references, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<ReviseAssuranceReport, AssuranceReportRegistration>
{
    const string CommandKind = nameof(ReviseAssuranceReport);
    const string Episode = "current";

    public async ValueTask<Result<AssuranceReportRegistration>> HandleAsync(
        IRequestContext<ReviseAssuranceReport> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProviderId == Uuid.Empty ||
            request.ReportId == Uuid.Empty || request.ExpectedRevision < 1)
            return Fail(RequestErrorKind.Validation,
                "An assurance report revision requires nonempty identities and the expected revision.");
        if (AssuranceRules.InputError(request.Content) is { } inputError)
            return Fail(RequestErrorKind.Validation, inputError);
        var normalized = AssuranceRules.Normalize(request.Content);
        if (AssuranceRules.Validate(normalized) is { } error)
            return Fail(RequestErrorKind.Validation, error);
        var actor = ProviderActor.From(context);
        var now = clock.GetUtcNow();
        var provenance = Owner(request).Provenance(request.CommandId, CommandKind, actor, Episode,
            (ulong)request.ExpectedRevision, AuthoredIntentDigest.Compute(request with { Content = normalized }), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        var prior = await reader.HydrateAsync(Owner(request), ct).ConfigureAwait(false);
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? contentRef = null;
        ImmutableContentRef? resultRef = null;
        var providerRevision = 0L;
        if (prior.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var provider = await references.ProviderAsync(request.TenantId, request.ProviderId, ct)
                .ConfigureAwait(false);
            if (!provider.IsSuccess)
                return Result<AssuranceReportRegistration>.Failure(provider.Error);
            var prepared = prior.PrepareRevise(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return Result<AssuranceReportRegistration>.Failure(prepared.Error!);
            var artifact = await references.ArtifactAsync(request.TenantId, normalized.Citation, ct)
                .ConfigureAwait(false);
            if (!artifact.IsSuccess)
                return Result<AssuranceReportRegistration>.Failure(artifact.Error);
            providerRevision = provider.Value.Revision;
            contentRef = await ProviderAssuranceContent.WriteAsync(content, request.TenantId,
                ProviderAssuranceContent.ReportRevisedSchema, normalized, ct).ConfigureAwait(false);
            resultRef = await ProviderAssuranceContent.WriteAsync(content, request.TenantId,
                ProviderAssuranceContent.ReportRegistrationSchema,
                new AssuranceReportRegistration(request.ReportId, request.ExpectedRevision + 1), ct)
                .ConfigureAwait(false);
            response = ProviderAssuranceContent.Completed(resultRef, ProviderAssuranceContent.ReportRegistrationSchema);
        }
        var accepted = await operations.ExecuteAsync(new NativeOperation<ProviderAssuranceReport>(binding,
            () => Owner(request), owner => owner.Acceptance,
            owner =>
            {
                var checkedRevision = owner.PrepareRevise(request.ExpectedRevision);
                return checkedRevision.IsSuccess && response is not null
                    ? Result<FirstResponseDescriptor>.Success(response)
                    : Result<FirstResponseDescriptor>.Failure(checkedRevision.IsSuccess
                        ? AcceptedAppendResult.ConflictError
                        : checkedRevision.Error!);
            },
            (ProviderAssuranceReport owner, ImmutableContentRef frozen) => owner.Revise(provenance,
                request.ExpectedRevision, contentRef!, providerRevision, actor, now, frozen, resultRef!)),
            context, ct).ConfigureAwait(false);
        return await ProviderAssuranceContent.ReadFirstResponseAsync<AssuranceReportRegistration>(content,
            request.TenantId, ProviderAssuranceContent.ReportRegistrationSchema, accepted, ct).ConfigureAwait(false);
    }

    static ProviderAssuranceReport Owner(ReviseAssuranceReport request) =>
        new(request.TenantId, request.ProviderId, request.ReportId, request.CommandId);

    static Result<AssuranceReportRegistration> Fail(RequestErrorKind kind, string message) =>
        Result<AssuranceReportRegistration>.Failure(new RequestError(kind, message));
}
