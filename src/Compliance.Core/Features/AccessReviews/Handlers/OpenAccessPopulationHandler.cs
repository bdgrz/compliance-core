using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessReviews.Handlers;

/// <summary>
///     Opens a draft against the exact current revision of an active system instance. The population id is the
///     logical operation identity, so an exact retry returns the original registration even after later facts or
///     acceptance, and the same id with a changed intent conflicts.
/// </summary>
public sealed class OpenAccessPopulationHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IDomainEventReader events, IImmutableContentStore content, TimeProvider clock,
    RestrictedApplicationVisibility visibility)
    : IRequestHandler<OpenAccessPopulation, AccessPopulationRegistration>
{
    const string CommandKind = "OpenAccessPopulation";

    public async ValueTask<Result<AccessPopulationRegistration>> HandleAsync(
        IRequestContext<OpenAccessPopulation> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.PopulationId == Uuid.Empty)
            return AccessReviewOutcome.Failure<AccessPopulationRegistration>(RequestErrorKind.Validation,
                "A population requires its immutable population id.");
        var instance = await ScopedSystemInstanceSource.FindAsync(reader, events, request.TenantId,
            request.ApplicationId, request.SystemInstanceId, ct).ConfigureAwait(false);
        if (instance is null)
            return AccessReviewOutcome.Failure<AccessPopulationRegistration>(
                RequestErrorKind.NotFound, "The system instance was not found.");
        var actor = AccessReviewActor.From(context);
        if (!await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId,
                request.ApplicationId, request.SystemInstanceId, ct).ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessPopulationRegistration>(
                RequestErrorKind.NotFound, "The system instance was not found.");

        var now = clock.GetUtcNow();
        var stream = new AccessPopulation(request.TenantId, request.PopulationId).Stream;
        var provenance = AccessReviewOperation.Provenance(request.PopulationId, CommandKind, stream,
            actor.Reference, null, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        var current = await reader.HydrateAsync(new AccessPopulation(request.TenantId, request.PopulationId,
            request.PopulationId), ct).ConfigureAwait(false);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (current.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = current.PrepareOpen();
            if (!prepared.IsSuccess)
                return Result<AccessPopulationRegistration>.Failure(prepared.Error!);
            if (instance.IsRetired)
                return AccessReviewOutcome.Failure<AccessPopulationRegistration>(
                    RequestErrorKind.Conflict, "A retired system instance cannot receive a new population.");
            if (instance.Revision != request.ExpectedSystemInstanceRevision)
                return AccessReviewOutcome.Failure<AccessPopulationRegistration>(
                    RequestErrorKind.Conflict,
                    $"The system instance is at revision {instance.Revision}; the population names revision {request.ExpectedSystemInstanceRevision}.");
            if (!AccessReviewVocabulary.IsBoundedText(request.Source, 2000))
                return AccessReviewOutcome.Failure<AccessPopulationRegistration>(RequestErrorKind.Validation,
                    "A population requires a source description of at most 2000 characters.");
            if (request.ObservedAt > now)
                return AccessReviewOutcome.Failure<AccessPopulationRegistration>(RequestErrorKind.Validation,
                    "A population cannot be observed in the future.");
            var body = await AccessReviewContent.WriteAsync(content, request.TenantId,
                AccessReviewContent.RegistrationSchema, new AccessPopulationRegistration(request.PopulationId, 1),
                ct).ConfigureAwait(false);
            result = await AccessReviewContent.WriteResultAsync(content, request.TenantId,
                AccessReviewContent.RegistrationSchema, body, ct).ConfigureAwait(false);
            response = AccessReviewContent.OkResponse(AccessReviewContent.RegistrationSchema, body);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AccessPopulation>(binding,
            () => new AccessPopulation(request.TenantId, request.PopulationId, request.PopulationId),
            population => population.Acceptance,
            population => AccessReviewOperation.Prepare(population.PrepareOpen(), response,
                "The population changed while the opening was prepared; reload and retry."),
            (AccessPopulation population, ImmutableContentRef frozen) => population.Open(provenance, request.ApplicationId, request.SystemInstanceId,
                instance.Revision, request.ObservedAt, request.Source.Trim(), actor.Reference, now, frozen, result!)),
            context, ct).ConfigureAwait(false);
        return await AccessReviewContent.ResponseAsync<AccessPopulationRegistration>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }
}
