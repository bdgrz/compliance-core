using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

/// <summary>Records refusal only against two actual committed source facts and lets the name owner decide the exact release.</summary>
public sealed class RefuseMethodologyTemplateCreationHandler(IAggregateExecutor executor, ISourceFactResolver sources,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<RefuseMethodologyTemplateCreation>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RefuseMethodologyTemplateCreation> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Only an internal creation reaction may refuse its admitted intent."));
        if (request.TemplateId == Uuid.Empty || !MethodologyTemplateCreationSources.IsClaimReference(request.Claim) ||
            request.ExistingCreation is null || request.ExistingCreation.EventId == Uuid.Empty ||
            request.ExistingCreation.Stream != new MethodologyTemplate(request.TemplateId).Stream ||
            request.CommandId != MethodologyTemplateCreationSources.RefusalId(request.Claim.EventId, request.ExistingCreation.EventId))
            return MethodologyTemplateCreationSources.Invalid();
        var admission = await MethodologyTemplateCreationSources.ResolveAsync(sources, content, request.TemplateId,
            request.Claim, request.ClaimPosition, ct).ConfigureAwait(false);
        if (admission is null)
            return MethodologyTemplateCreationSources.Invalid();
        var existing = await sources.ResolveAsync(request.ExistingCreation.Stream, request.ExistingPosition, ct).ConfigureAwait(false);
        return existing.Event switch
        {
            MethodologyTemplateCreatedV4 => await RefuseAsync(context, admission, ct).ConfigureAwait(false),
            MethodologyTemplateCreated => await RefuseLegacyAsync(context, admission, ct).ConfigureAwait(false),
            _ => MethodologyTemplateCreationSources.Invalid(),
        };
    }

    async ValueTask<Result> RefuseAsync(IRequestContext<RefuseMethodologyTemplateCreation> context,
        MethodologyTemplateCreationSources.Admission admission, CancellationToken ct)
    {
        var request = context.Request;
        var (proof, existing, _) = await SourceFactProofs.BuildAsync<MethodologyTemplateCreatedV4>(sources, content,
            MethodologyTemplate.Realm, request.ExistingCreation, request.ExistingPosition, ct).ConfigureAwait(false);
        var proofs = new MethodologyTemplateCreationConflictProofs(admission.Proof, proof);
        var snapshot = await content.WriteJsonAsync(MethodologyTemplate.Realm,
            Uuid.CreateVersion5(request.CommandId, MethodologyTemplateNameClaim.ConflictSchema),
            MethodologyTemplateNameClaim.ConflictSchema, proofs, ct).ConfigureAwait(false);
        var operation = Provenance(context.Request, admission.Claim, snapshot);
        return await executor.ExecuteAsync(new MethodologyTemplateNameClaim(admission.Intent.Name, request.CommandId),
            name => AggregateOutcome.CommitOnSuccess(name.RefuseCreation(operation, admission.Claim, admission.Intent,
                existing, proofs, snapshot)), context, ct).ConfigureAwait(false);
    }

    async ValueTask<Result> RefuseLegacyAsync(IRequestContext<RefuseMethodologyTemplateCreation> context,
        MethodologyTemplateCreationSources.Admission admission, CancellationToken ct)
    {
        var request = context.Request;
        var (proof, existing, _) = await SourceFactProofs.BuildAsync<MethodologyTemplateCreated>(sources, content,
            MethodologyTemplate.Realm, request.ExistingCreation, request.ExistingPosition, ct).ConfigureAwait(false);
        var proofs = new MethodologyTemplateLegacyCreationConflictProofs(admission.Proof, proof);
        var snapshot = await content.WriteJsonAsync(MethodologyTemplate.Realm,
            Uuid.CreateVersion5(request.CommandId, MethodologyTemplateNameClaim.LegacyConflictSchema),
            MethodologyTemplateNameClaim.LegacyConflictSchema, proofs, ct).ConfigureAwait(false);
        var operation = Provenance(request, admission.Claim, snapshot);
        return await executor.ExecuteAsync(new MethodologyTemplateNameClaim(admission.Intent.Name, request.CommandId),
            name => AggregateOutcome.CommitOnSuccess(name.RefuseLegacyCreation(operation, admission.Claim, admission.Intent,
                existing, proofs, snapshot)), context, ct).ConfigureAwait(false);
    }

    OperationProvenance Provenance(RefuseMethodologyTemplateCreation request, MethodologyTemplateNameClaimed claim,
        ImmutableContentRef proofContent) => new(request.CommandId, claim.Operation.RootOperationId,
        request.ExistingCreation.EventId, nameof(RefuseMethodologyTemplateCreation),
        ActorReference.ForSystemProcess(MethodologyTemplateNameClaim.ProcessId, "Methodology template creation"),
        request.Claim.Stream, request.TemplateId.ToString(), null, proofContent.Sha256, clock.GetUtcNow(), claim.Owner, claim.Owner);
}
