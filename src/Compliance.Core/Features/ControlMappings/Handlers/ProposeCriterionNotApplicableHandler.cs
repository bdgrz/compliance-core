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

    public ValueTask<Result<CriterionApplicabilityRegistration>> HandleAsync(
        IRequestContext<ProposeCriterionNotApplicable> context, CancellationToken ct) =>
        MappingLineFailures.ExecuteAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result<CriterionApplicabilityRegistration>> HandleCoreAsync(
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

        MappingLinePrepared? prepared = null;
        var outcome = await operations.ExecuteAsync(new NativeOperation<CriterionApplicability>(binding,
            () => new CriterionApplicability(request.TenantId, request.ProgramId, request.EditionId, request.CriterionIdentifier, request.DecisionId), owner => owner.Acceptance,
            async (owner, token) =>
            {
                var rules = await applicability.ComposeAsync(owner, token).ConfigureAwait(false);
                FirstResponseDescriptor? response = null;
                ImmutableContentRef? result = null;
                var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId, request.ProgramId), token)
                    .ConfigureAwait(false);
                if (!program.IsCreated)
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound, "The program was not found."));
                if (program.CriteriaEditionId != request.EditionId)
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "A not-applicable decision must target the criteria edition the program has selected."));
                if (catalog.GetEntry(request.EditionId, request.CriterionIdentifier) is not { Kind: "criterion" })
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Validation,
                        "Not-applicable decisions apply to a criterion of the selected edition, not a point of focus."));
                var guard = rules.PrepareProposal(request.ExpectedRevision, request.Rationale);
                if (!guard.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(guard.Error!);
                (response, result) = await NativeContent.FreezeAsync(content, request.TenantId,
                    CriterionApplicability.RegistrationSchema, new CriterionApplicabilityRegistration(request.DecisionId,
                        request.ExpectedRevision + 1, (rules.View?.Versions.Count ?? 0) + 1), token).ConfigureAwait(false);

                var applied = rules.Propose(provenance, request.Rationale, actor.MemberId, member, now);
                if (!applied.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(applied.Error!);
                prepared = await MappingLineContent.PrepareAsync(content, owner, request, rules.Changes[^1],
                    MappingLineContent.Current(rules), MappingLineContent.Header(rules), result!, token).ConfigureAwait(false);
                return Result<FirstResponseDescriptor>.Success(response!);
            }, (owner, frozen) => owner.Apply(prepared ?? throw new InvalidOperationException("The native line was not prepared."), frozen)),
            context, ct).ConfigureAwait(false);
        return await NativeContent.ResponseAsync<CriterionApplicabilityRegistration>(content, request.TenantId, outcome, ct).ConfigureAwait(false);
    }

    static Result<CriterionApplicabilityRegistration> Failure(RequestErrorKind kind, string message) =>
        Result<CriterionApplicabilityRegistration>.Failure(new RequestError(kind, message));
}
