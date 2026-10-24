using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     Proposes that one criterion of the program's selected edition does not apply; HTTP-only. The proposal id is
///     the logical operation identity, so an exact retry returns the original registration even after the proposal
///     was reviewed, and the same id with a changed rationale conflicts. Different criteria never touch one another.
/// </summary>
public sealed class ProposeCriterionNotApplicableHandler(NativeOperationExecutor operations,
    ApplicabilityReader applicability, IAggregateReader reader, ICriteriaCatalog catalog,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<ProposeCriterionNotApplicable, CriterionApplicabilityRegistration>
{
    const string CommandKind = "ProposeCriterionNotApplicable";

    public async ValueTask<Result<CriterionApplicabilityRegistration>> HandleAsync(
        IRequestContext<ProposeCriterionNotApplicable> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Failure(RequestErrorKind.Forbidden, "Criterion sign-off requires personal HTTP submission.");
        var request = context.Request;
        if (request.DecisionId == Uuid.Empty || request.EditionId == Uuid.Empty || request.ExpectedRevision < 0 ||
            string.IsNullOrWhiteSpace(request.CriterionIdentifier))
            return Failure(RequestErrorKind.Validation,
                "A not-applicable proposal requires a decision id, an edition, a criterion and a non-negative expected revision.");

        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var member = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var line = await applicability.ReadLineAsync(request.TenantId, request.ProgramId, request.EditionId,
            request.CriterionIdentifier, request.DecisionId, ct).ConfigureAwait(false);
        var provenance = RootOperation.Provenance(request.DecisionId, CommandKind, line.Stream, member,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (line.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId, request.ProgramId), ct)
                .ConfigureAwait(false);
            if (!program.IsCreated)
                return Failure(RequestErrorKind.NotFound, "The program was not found.");
            if (program.CriteriaEditionId != request.EditionId)
                return Failure(RequestErrorKind.Conflict,
                    "A not-applicable decision must target the criteria edition the program has selected.");
            if (catalog.GetEntry(request.EditionId, request.CriterionIdentifier) is not { Kind: "criterion" })
                return Failure(RequestErrorKind.Validation,
                    "Not-applicable decisions apply to a criterion of the selected edition, not a point of focus.");
            var prepared = line.PrepareProposal(request.ExpectedRevision, request.Rationale);
            if (!prepared.IsSuccess)
                return Result<CriterionApplicabilityRegistration>.Failure(prepared.Error!);
            (response, result) = await NativeContent.FreezeAsync(content, request.TenantId,
                CriterionApplicability.RegistrationSchema, new CriterionApplicabilityRegistration(request.DecisionId,
                    request.ExpectedRevision + 1, (line.View?.Versions.Count ?? 0) + 1), ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<CriterionApplicability>(binding,
            () => new CriterionApplicability(request.TenantId, request.ProgramId, request.EditionId,
                request.CriterionIdentifier, request.DecisionId), owner => owner.Acceptance,
            owner => Prepare(owner, request, response),
            (CriterionApplicability owner, ImmutableContentRef frozen) => owner.Propose(provenance,
                request.Rationale, actor.MemberId, member, now, frozen, result!)), context, ct).ConfigureAwait(false);
        return await NativeContent.ResponseAsync<CriterionApplicabilityRegistration>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }

    static Result<FirstResponseDescriptor> Prepare(CriterionApplicability owner, ProposeCriterionNotApplicable request,
        FirstResponseDescriptor? response)
    {
        var prepared = owner.PrepareProposal(request.ExpectedRevision, request.Rationale);
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return response is null
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The applicability line changed while the proposal was prepared; reload and retry.", true))
            : Result<FirstResponseDescriptor>.Success(response);
    }

    static Result<CriterionApplicabilityRegistration> Failure(RequestErrorKind kind, string message) =>
        Result<CriterionApplicabilityRegistration>.Failure(new RequestError(kind, message));
}
