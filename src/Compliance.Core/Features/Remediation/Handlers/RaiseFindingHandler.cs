using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Readiness.Readers;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Remediation.Handlers;

/// <summary>
///     Records a finding. A governed source must exist in this program: a readiness gap names its assessment as the
///     source version, and a control occurrence must have been recorded. The finding identity is the logical
///     operation, so an exact retry replays the original registration even after the finding changed.
/// </summary>
public sealed class RaiseFindingHandler(NativeOperationExecutor operations, IAggregateReader reader,
    ControlOperationsReader operatingReader, Readiness.Readers.ReadinessReader readiness, IImmutableContentStore content,
    TimeProvider clock)
    : IRequestHandler<RaiseFinding, FindingRegistration>
{
    public async ValueTask<Result<FindingRegistration>> HandleAsync(IRequestContext<RaiseFinding> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.FindingId == Uuid.Empty)
            return Result<FindingRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "A finding requires its own identity."));
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var actorReference = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var snapshot = new FindingAggregate(request.TenantId, request.ProgramId,
            request.FindingId, request.FindingId);
        var provenance = FindingContent.Provenance(request.FindingId, "RaiseFinding", snapshot.Stream, actorReference,
            null, FindingCommands.Digest(request), now) with
        { FirstOwner = snapshot.Stream };
        var binding = AcceptedOperationBinding.Of(provenance);

        FindingRaiseTerms? terms = null;
        ImmutableContentRef? links = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(FindingAggregate owner, CancellationToken token)
        {
            var failure = FindingRules.ValidateRaise(request.Source, request.Title, request.Description, request.Severity,
                request.AffectedScope, request.OwnerMemberId, request.DueOn, request.Links ?? []);
            if (failure is not null)
                return Result<FirstResponseDescriptor>.Failure(failure.ToRequestError());
            var prepared = owner.PrepareRaise();
            if (!prepared.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            if (await SourceErrorAsync(request, token).ConfigureAwait(false) is { } sourceError)
                return Result<FirstResponseDescriptor>.Failure(sourceError);
            if (!await FindingCommands.IsActiveMemberAsync(reader, request.TenantId, request.OwnerMemberId, token)
                    .ConfigureAwait(false))
                return Result<FirstResponseDescriptor>.Failure(FindingCommands.InactiveOwner());
            terms = new FindingRaiseTerms(request.Source, request.Title.Trim(), request.Description.Trim(),
                request.Severity, request.AffectedScope.Trim(), request.OwnerMemberId, request.DueOn, actorReference,
                now, "manual", null);
            links = await FindingContent.WriteAsync(content, request.TenantId, FindingContent.LinksSchema,
                FindingRules.Trimmed(request.Links ?? []), token).ConfigureAwait(false);
            (response, result) = await FindingContent.FreezeAsync(content, request.TenantId,
                FindingContent.RegistrationSchema, new FindingRegistration(request.FindingId, 1), token)
                .ConfigureAwait(false);
            return Result<FirstResponseDescriptor>.Success(response!);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<FindingAggregate>(binding,
            () => new FindingAggregate(request.TenantId, request.ProgramId, request.FindingId, request.FindingId),
            finding => finding.Acceptance, PrepareAsync,
            (FindingAggregate finding, ImmutableContentRef frozen) =>
                finding.Raise(provenance, terms!, links!, frozen, result!, null)), context, ct).ConfigureAwait(false);
        return await FindingContent.ResponseAsync<FindingRegistration>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }

    async ValueTask<RequestError?> SourceErrorAsync(RaiseFinding request, CancellationToken ct)
    {
        var source = request.Source;
        if (source?.RecordId is not { } recordId)
            return null;
        var missing = new RequestError(RequestErrorKind.Validation,
            "The finding source record was not found in this program.");
        switch (source.Kind)
        {
            case "readiness_gap":
                if (!Uuid.TryParse(source.Version, null, out var assessmentId))
                    return missing;
                try
                {
                    var program = await readiness.ReadProgramAsync(request.TenantId, request.ProgramId, ct)
                        .ConfigureAwait(false);
                    return program.AdoptionOf(assessmentId) is not null &&
                           await readiness.ReadContentAsync(request.TenantId, request.ProgramId, assessmentId, ct)
                               .ConfigureAwait(false) is { } loaded && loaded.Gaps.Any(gap => gap.GapId == recordId)
                        ? null
                        : missing;
                }
                catch (ImmutableContentIntegrityException exception)
                {
                    return new RequestError(RequestErrorKind.Conflict,
                        $"The readiness content cannot be verified yet: {exception.Message}", true);
                }

            case "control_occurrence" or "occurrence_review":
                var snapshot = await operatingReader.ReadAsync(request.TenantId, request.ProgramId, ct)
                    .ConfigureAwait(false);
                return snapshot.OccurrenceControlId(recordId) is null ? missing : null;
            default:
                return null;
        }
    }
}
