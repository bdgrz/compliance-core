using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Records a finding. A governed source must exist in this program: a readiness gap names its assessment as the
///     source version, and a control occurrence must have been recorded. The finding identity is the logical
///     operation, so an exact retry replays the original registration even after the finding changed.
/// </summary>
public sealed class RaiseFindingHandler(NativeOperationExecutor operations, IAggregateReader reader,
    ControlOperationsReader operatingReader, Readiness.ReadinessReader readiness, IImmutableContentStore content,
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
        var snapshot = await reader.HydrateAsync(new FindingAggregate(request.TenantId, request.ProgramId,
            request.FindingId, request.FindingId), ct).ConfigureAwait(false);
        var provenance = FindingContent.Provenance(request.FindingId, "RaiseFinding", snapshot.Stream, actorReference,
            null, FindingCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        FindingRaiseTerms? terms = null;
        ImmutableContentRef? links = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var failure = FindingRules.ValidateRaise(request.Source, request.Title, request.Description, request.Severity,
                request.AffectedScope, request.OwnerMemberId, request.DueOn, request.Links ?? []);
            if (failure is not null)
                return Result<FindingRegistration>.Failure(failure.ToRequestError());
            var prepared = snapshot.PrepareRaise();
            if (!prepared.IsSuccess)
                return Result<FindingRegistration>.Failure(prepared.Error!);
            if (await SourceErrorAsync(request, ct).ConfigureAwait(false) is { } sourceError)
                return Result<FindingRegistration>.Failure(sourceError);
            if (!await FindingCommands.IsActiveMemberAsync(reader, request.TenantId, request.OwnerMemberId, ct)
                    .ConfigureAwait(false))
                return Result<FindingRegistration>.Failure(FindingCommands.InactiveOwner());
            terms = new FindingRaiseTerms(request.Source, request.Title.Trim(), request.Description.Trim(),
                request.Severity, request.AffectedScope.Trim(), request.OwnerMemberId, request.DueOn, actorReference,
                now, "manual", null);
            links = await FindingContent.WriteAsync(content, request.TenantId, FindingContent.LinksSchema,
                FindingRules.Trimmed(request.Links ?? []), ct).ConfigureAwait(false);
            (response, result) = await FindingContent.FreezeAsync(content, request.TenantId,
                FindingContent.RegistrationSchema, new FindingRegistration(request.FindingId, 1), ct)
                .ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<FindingAggregate>(binding,
            () => new FindingAggregate(request.TenantId, request.ProgramId, request.FindingId, request.FindingId),
            finding => finding.Acceptance, finding => FindingCommands.Prepared(finding.PrepareRaise(), response),
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
