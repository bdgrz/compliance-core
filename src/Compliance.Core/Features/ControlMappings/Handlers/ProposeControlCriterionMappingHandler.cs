using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.ControlMappings.Aggregates;
using Bdgrz.Compliance.Features.ControlMappings.Commands;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.ControlMappings.Readers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
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

namespace Bdgrz.Compliance.Features.ControlMappings.Handlers;

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

    public ValueTask<Result<ControlCriterionMappingRegistration>> HandleAsync(
        IRequestContext<ProposeControlCriterionMapping> context, CancellationToken ct) =>
        MappingLineFailures.ExecuteAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result<ControlCriterionMappingRegistration>> HandleCoreAsync(
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

        MappingLinePrepared? prepared = null;
        var outcome = await operations.ExecuteAsync(new NativeOperation<ControlCriterionMapping>(binding,
            () => new ControlCriterionMapping(request.TenantId, request.ProgramId, request.ControlId, request.EditionId, request.CriterionIdentifier, request.MappingId), owner => owner.Acceptance,
            async (owner, token) =>
            {
                var rules = await mappings.ComposeAsync(owner, token).ConfigureAwait(false);
                FirstResponseDescriptor? response = null;
                ImmutableContentRef? result = null;
                var criterionKind = string.Empty;
                var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId, request.ProgramId), token)
                    .ConfigureAwait(false);
                if (!program.IsCreated)
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound, "The program was not found."));
                if (program.CriteriaEditionId != request.EditionId)
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "A mapping must target the criteria edition the program has selected."));
                var entry = catalog.GetEntry(request.EditionId, request.CriterionIdentifier);
                if (entry is null)
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Validation,
                        "The criterion or point of focus is not in the selected edition."));
                var control = await controls.LoadAsync(request.TenantId, request.ProgramId, request.ControlId, token)
                    .ConfigureAwait(false);
                if (!control.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(control.Error);
                if (control.Value.ApprovedVersion?.VersionId != request.ControlVersionId)
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "A mapping must reference an approved version of the control."));
                var guard = rules.PrepareProposal(request.ControlVersionId, request.ExpectedRevision, request.Rationale,
                    request.ApplicabilityExplanation);
                if (!guard.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(guard.Error!);
                criterionKind = entry.Kind;
                (response, result) = await NativeContent.FreezeAsync(content, request.TenantId,
                    ControlCriterionMapping.RegistrationSchema, new ControlCriterionMappingRegistration(request.MappingId,
                        request.ExpectedRevision + 1, (rules.View?.Versions.Count ?? 0) + 1), token).ConfigureAwait(false);

                var applied = rules.Propose(provenance, request.ControlVersionId, criterionKind, request.Rationale, request.ApplicabilityExplanation, actor.MemberId, actor.Display, now);
                if (!applied.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(applied.Error!);
                prepared = await MappingLineContent.PrepareAsync(content, owner, request, rules.Changes[^1],
                    MappingLineContent.Current(rules), MappingLineContent.Header(rules), result!, token).ConfigureAwait(false);
                return Result<FirstResponseDescriptor>.Success(response!);
            }, (owner, frozen) => owner.Apply(prepared ?? throw new InvalidOperationException("The native line was not prepared."), frozen)),
            context, ct).ConfigureAwait(false);
        return await NativeContent.ResponseAsync<ControlCriterionMappingRegistration>(content, request.TenantId, outcome, ct).ConfigureAwait(false);
    }

    static Result<ControlCriterionMappingRegistration> Failure(RequestErrorKind kind, string message) =>
        Result<ControlCriterionMappingRegistration>.Failure(new RequestError(kind, message));
}
