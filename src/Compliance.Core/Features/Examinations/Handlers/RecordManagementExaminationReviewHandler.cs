using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Examinations.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.AuditPackages.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Bdgrz.Compliance.Features.Operations.Readers;
namespace Bdgrz.Compliance.Features.Examinations.Handlers;

/// <summary>Records an immutable management decision after resolving its exact examination sources.</summary>
public sealed class RecordManagementExaminationReviewHandler(IAggregateExecutor executor,
    IAggregateReader reader, AuditPackageReader packages, TypeIIPeriodReader periods, ManagementExecutiveAuthority executiveAuthority, TimeProvider clock, ExaminationEngagementReader examinations)
    : IRequestHandler<RecordManagementExaminationReview>
{
    public ValueTask<Result> HandleAsync(
        IRequestContext<RecordManagementExaminationReview> context, CancellationToken ct) =>
        ExaminationSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result> HandleCoreAsync(
        IRequestContext<RecordManagementExaminationReview> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var memberId, out var display) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Failure(RequestErrorKind.Forbidden,
                "An examination review requires a personal management actor.");
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.ExaminationId == Uuid.Empty || request.SignOffId == Uuid.Empty ||
            request.Content is null || request.Content.ExaminationId != request.ExaminationId)
            return Failure(RequestErrorKind.Validation,
                "An examination review requires matching examination and sign-off identities with complete content.");

        var role = await executiveAuthority.ResolveSignerRoleAsync(request.TenantId,
            request.ProgramId, userId, memberId, request.Content.Approvers, ct)
            .ConfigureAwait(false);
        if (role is null)
            return Failure(RequestErrorKind.Forbidden,
                "An examination review requires one current program grant matching the actor's named CEO or CISO role.");

        var aggregateId = new ExaminationSignOff(request.TenantId, request.ProgramId,
            request.SignOffId);
        var existing = await reader.HydrateAsync(aggregateId, ct).ConfigureAwait(false);
        if (existing.Current is { } prior)
            return await executor.ExecuteAsync(aggregateId, aggregate =>
                    CommandFailureRequestAdapter.ToOutcome(aggregate.SignOff(request.Content,
                        memberId, role, display, prior.SignedOffAt)), context, ct)
                .ConfigureAwait(false);

        var sources = await ExaminationSignOffSourceValidator.ValidateAsync(reader, packages, periods, examinations,
            request.TenantId, request.ProgramId, request.Content, ct).ConfigureAwait(false);
        if (!sources.IsSuccess)
            return Result.Failure(sources.Error!);

        var signedOffAt = clock.GetUtcNow();
        if (signedOffAt == default || signedOffAt < sources.Value)
            return Failure(RequestErrorKind.Conflict,
                "The management review cannot precede its latest delivered or approved source record.");

        return await executor.ExecuteAsync(aggregateId, aggregate =>
                CommandFailureRequestAdapter.ToOutcome(aggregate.SignOff(request.Content,
                    memberId, role, display, signedOffAt)), context, ct)
            .ConfigureAwait(false);
    }

    static Result Failure(RequestErrorKind kind, string message) =>
        Result.Failure(new RequestError(kind, message));
}
