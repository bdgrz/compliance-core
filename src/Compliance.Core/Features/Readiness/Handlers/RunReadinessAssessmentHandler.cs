using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     Admits a readiness run on the program readiness owner at <c>ExpectedProgramRevision</c>. Admission reads the
///     program's criteria edition selected at the requested time, mapping ledger, mapped controls and canonical source
///     records once, freezes those complete inputs and the as-of time (the server time when the caller omitted it)
///     as immutable content, and reserves the one pending latest-adoption generation before any computation. The 202
///     acknowledgment is frozen on the admission, so an exact retry replays it after the run completed and after later
///     runs. A source that is not readable or consistent yet fails the admission as transient; a missing business input
///     is recorded as an explicit gap by the computation, never as a partial assessment.
/// </summary>
public sealed class RunReadinessAssessmentHandler(NativeOperationExecutor operations, ProgramReader programs, ControlReader controlSources,
    ControlMappingReader mappingReader, ICriteriaCatalog catalog, IReadinessSourceReader sources,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<RunReadinessAssessment, OperationAcceptedResponse>
{
    const string CommandKind = "RunReadinessAssessment";

    public ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<RunReadinessAssessment> context, CancellationToken ct) =>
        ControlSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<OperationAcceptedResponse>> HandleCoreAsync(
        IRequestContext<RunReadinessAssessment> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.AssessmentId == Uuid.Empty || request.ExpectedProgramRevision < 0)
            return Failure(RequestErrorKind.Validation,
                "A readiness run requires its own assessment identity and a non-negative expected program revision.");
        var actor = ReadinessActor.From(context, request.TenantId);
        var actorReference = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var stream = new ProgramReadiness(request.TenantId, request.ProgramId).Stream;
        var provenance = ReadinessContent.Provenance(request.AssessmentId, CommandKind, stream, actorReference,
            (ulong)request.ExpectedProgramRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? intent = null;

        var outcome = await operations.ExecuteAsync(new NativeOperation<ProgramReadiness>(binding,
            () => new ProgramReadiness(request.TenantId, request.ProgramId, request.AssessmentId),
            readiness => readiness.Acceptance,
            async (readiness, token) =>
            {
                var frozen = await FreezeAsync(request, readiness, provenance, actor, now, token).ConfigureAwait(false);
                if (!frozen.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(frozen.Error!);
                intent = frozen.Value.Intent;
                return Result<FirstResponseDescriptor>.Success(frozen.Value.Response);
            },
            (ProgramReadiness readiness, ImmutableContentRef acknowledgment) =>
                readiness.RequestRun(provenance, intent!, acknowledgment)), context, ct).ConfigureAwait(false);
        return await ReadinessContent.ResponseAsync<OperationAcceptedResponse>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }

    async ValueTask<Result<(ImmutableContentRef Intent, FirstResponseDescriptor Response)>> FreezeAsync(
        RunReadinessAssessment request, ProgramReadiness readiness, OperationProvenance provenance, ReadinessActor actor,
        DateTimeOffset now, CancellationToken ct)
    {
        var asOf = request.AsOf ?? now;
        if (asOf > now)
            return Fail(RequestErrorKind.Validation, "A readiness assessment cannot be as of a future time.");
        var prepared = readiness.PrepareRun(request.ExpectedProgramRevision);
        if (!prepared.IsSuccess)
            return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(prepared.Error!);
        var tenant = request.TenantId;
        var program = await programs.HistoryAsync(tenant, request.ProgramId, ct).ConfigureAwait(false);
        if (!program.Owner.IsCreated)
            return Fail(RequestErrorKind.NotFound, "The program was not found.");
        var selectedEditionId = program.CriteriaSelectionAt(asOf)?.EditionId;
        var catalogEdition = selectedEditionId is { } selected ? catalog.GetEdition(selected) : null;
        var editionId = catalogEdition is not null ? selectedEditionId : null;
        var criteria = editionId is { } edition
            ? catalog.ListEntries(edition, null, "criterion", null)
            : [];
        var mappings = (await mappingReader.ReadProgramAsync(request.TenantId, request.ProgramId, ct)
                .ConfigureAwait(false)).Mappings.Where(mapping => mapping.EditionId == editionId).ToArray();
        var controls = new List<ReadinessEffectiveControl>();
        foreach (var controlId in mappings
                     .Where(mapping => ReadinessRules.ActiveAt(mapping, asOf) is not null)
                     .Select(static mapping => mapping.ControlId).Distinct())
        {
            var control = await controlSources.ReadAsync(tenant, request.ProgramId, controlId, ct).ConfigureAwait(false);
            controls.Add(new ReadinessEffectiveControl(controlId,
                control.IsVisible && control.ProgramId == request.ProgramId
                    ? ReadinessControlSelection.EffectiveVersionAt(control, asOf)
                    : null));
        }

        var sourceResult = await sources.ReadAsync(tenant, request.ProgramId, asOf, ct).ConfigureAwait(false);
        if (!sourceResult.IsSuccess)
            return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(sourceResult.Error);

        var catalogReference = catalogEdition is null
            ? null
            : await ReadinessContent.WriteAsync(content, tenant, "readiness-input-snapshot.catalog-edition.v1",
                catalogEdition, ct).ConfigureAwait(false);
        var cuts = await ImmutableSelectionManifests.WriteAsync<AuthoritativeReadCut>(content, tenant.ToString(),
            Uuid.CreateVersion4(), "authoritative-read-cut.v1", [], SelectionOrder.Authored, ct).ConfigureAwait(false);
        var inputs = await ReadinessContent.WriteAsync(content, tenant, ReadinessInputSnapshot.Schema,
            new ReadinessInputSnapshot(request.ProgramId, asOf, editionId, catalogReference,
                await ReadinessContent.WriteAsync<IReadOnlyList<Criterion>>(content, tenant,
                    "readiness-input-snapshot.criteria.v1", criteria, ct).ConfigureAwait(false),
                await ReadinessContent.WriteAsync<IReadOnlyList<ControlCriterionMappingView>>(content, tenant,
                    "readiness-input-snapshot.mappings.v1", mappings, ct).ConfigureAwait(false),
                await ReadinessContent.WriteAsync<IReadOnlyList<ReadinessEffectiveControl>>(content, tenant,
                    "readiness-input-snapshot.effective-controls.v1", controls, ct).ConfigureAwait(false),
                await ReadinessContent.WriteAsync(content, tenant, "readiness-input-snapshot.sources.v1",
                    sourceResult.Value, ct).ConfigureAwait(false), cuts), ct).ConfigureAwait(false);
        var intent = await ReadinessContent.WriteAsync(content, tenant, ReadinessRunIntent.Schema,
            new ReadinessRunIntent(ReadinessContent.Header(provenance, now), request.ProgramId, request.AssessmentId,
                (ulong)request.ExpectedProgramRevision, (ulong)readiness.AdoptionGeneration + 1,
                ReadinessRules.Version, asOf, inputs, actor.MemberId), ct).ConfigureAwait(false);
        var acknowledgment = await ReadinessContent.AcceptedResponseAsync(content, tenant, request.AssessmentId,
            RegisterFirmStaffHandler.StatusUrlTemplate, now, ct).ConfigureAwait(false);
        return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Success((intent, acknowledgment));
    }

    static Result<(ImmutableContentRef, FirstResponseDescriptor)> Fail(RequestErrorKind kind, string message) =>
        Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(new RequestError(kind, message));

    static Result<OperationAcceptedResponse> Failure(RequestErrorKind kind, string message) =>
        Result<OperationAcceptedResponse>.Failure(new RequestError(kind, message));
}
