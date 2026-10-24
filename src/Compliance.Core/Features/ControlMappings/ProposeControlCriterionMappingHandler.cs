using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     Proposes a mapping on the line of one (control, edition, criterion) tuple after resolving the program's
///     selected edition, the catalog entry and the control's exact approved version. The proposal id is the logical
///     operation identity, so an exact retry returns the original registration even after the proposal was reviewed,
///     and the same id with a changed intent conflicts. Proposals on different tuples never touch one another.
/// </summary>
public sealed class ProposeControlCriterionMappingHandler(NativeOperationExecutor operations,
    ControlMappingReader mappings, IAggregateReader reader, ICriteriaCatalog catalog,
    ControlActivationSource controls, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<ProposeControlCriterionMapping, ControlCriterionMappingRegistration>
{
    const string CommandKind = "ProposeControlCriterionMapping";

    public async ValueTask<Result<ControlCriterionMappingRegistration>> HandleAsync(
        IRequestContext<ProposeControlCriterionMapping> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.MappingId == Uuid.Empty || request.ControlId == Uuid.Empty || request.EditionId == Uuid.Empty ||
            request.ExpectedRevision < 0 || string.IsNullOrWhiteSpace(request.CriterionIdentifier))
            return Failure(RequestErrorKind.Validation,
                "A mapping proposal requires a mapping id, control, edition, criterion and a non-negative expected revision.");

        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var now = clock.GetUtcNow();
        var line = await mappings.ReadLineAsync(request.TenantId, request.ProgramId, request.ControlId,
            request.EditionId, request.CriterionIdentifier, request.MappingId, ct).ConfigureAwait(false);
        var provenance = RootOperation.Provenance(request.MappingId, CommandKind, line.Stream,
            ActorReference.ForMember(actor.MemberId, actor.Display), request.ExpectedRevision,
            AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        var criterionKind = string.Empty;
        if (line.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId, request.ProgramId), ct)
                .ConfigureAwait(false);
            if (!program.IsCreated)
                return Failure(RequestErrorKind.NotFound, "The program was not found.");
            if (program.CriteriaEditionId != request.EditionId)
                return Failure(RequestErrorKind.Conflict,
                    "A mapping must target the criteria edition the program has selected.");
            var entry = catalog.GetEntry(request.EditionId, request.CriterionIdentifier);
            if (entry is null)
                return Failure(RequestErrorKind.Validation,
                    "The criterion or point of focus is not in the selected edition.");
            var control = await controls.LoadAsync(request.TenantId, request.ProgramId, request.ControlId, ct)
                .ConfigureAwait(false);
            if (!control.IsSuccess)
                return Result<ControlCriterionMappingRegistration>.Failure(control.Error);
            if (control.Value.ApprovedVersion?.VersionId != request.ControlVersionId)
                return Failure(RequestErrorKind.Conflict,
                    "A mapping must reference an approved version of the control.");
            var prepared = line.PrepareProposal(request.ControlVersionId, request.ExpectedRevision, request.Rationale,
                request.ApplicabilityExplanation);
            if (!prepared.IsSuccess)
                return Result<ControlCriterionMappingRegistration>.Failure(prepared.Error!);
            criterionKind = entry.Kind;
            (response, result) = await NativeContent.FreezeAsync(content, request.TenantId,
                ControlCriterionMapping.RegistrationSchema, new ControlCriterionMappingRegistration(request.MappingId,
                    request.ExpectedRevision + 1, (line.View?.Versions.Count ?? 0) + 1), ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<ControlCriterionMapping>(binding,
            () => new ControlCriterionMapping(request.TenantId, request.ProgramId, request.ControlId,
                request.EditionId, request.CriterionIdentifier, request.MappingId), owner => owner.Acceptance,
            owner => Prepare(owner, request, response),
            (ControlCriterionMapping owner, ImmutableContentRef frozen) => owner.Propose(provenance,
                request.ControlVersionId, criterionKind, request.Rationale, request.ApplicabilityExplanation,
                actor.MemberId, actor.Display, now, frozen, result!)), context, ct).ConfigureAwait(false);
        return await NativeContent.ResponseAsync<ControlCriterionMappingRegistration>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }

    static Result<FirstResponseDescriptor> Prepare(ControlCriterionMapping owner, ProposeControlCriterionMapping request,
        FirstResponseDescriptor? response)
    {
        var prepared = owner.PrepareProposal(request.ControlVersionId, request.ExpectedRevision, request.Rationale,
            request.ApplicabilityExplanation);
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return response is null
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The mapping line changed while the proposal was prepared; reload and retry.", true))
            : Result<FirstResponseDescriptor>.Success(response);
    }

    static Result<ControlCriterionMappingRegistration> Failure(RequestErrorKind kind, string message) =>
        Result<ControlCriterionMappingRegistration>.Failure(new RequestError(kind, message));
}
