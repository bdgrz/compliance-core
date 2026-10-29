using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Rules;

public sealed class IndependenceRuleCatalogOperations(NativeOperationExecutor operations, IndependenceRuleCatalogReader rules,
    IImmutableContentStore content, TimeProvider clock)
{
    public async ValueTask<Result<IndependenceRuleVersionView>> ReviseAsync(IRequestContext<ReviseIndependenceRules> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation, "A rule revision requires its authored command identity.");
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            throw new InvalidOperationException("IndependenceRuleAdministrationAuthorizer must reject this actor.");
        var actor = ActorReference.ForPlatformOperator(userId, UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
        var snapshot = new IndependenceRuleCatalog();
        var operation = OwnerProvenance.For(snapshot, request.CommandId, nameof(ReviseIndependenceRules), actor, snapshot.Id.ToString(),
            unchecked((ulong)request.ExpectedSequence), AuthoredIntentDigest.Compute(request), clock.GetUtcNow()) with
        { FirstOwner = snapshot.Stream };
        IndependenceRuleVersionContent? prepared = null;
        ImmutableContentRef? body = null;
        ImmutableContentRef? result = null;
        var sequence = 0L;
        var position = 0UL;
        try
        {
            var accepted = await operations.ExecuteAsync(new NativeOperation<IndependenceRuleCatalog>(AcceptedOperationBinding.Of(operation),
                () => new IndependenceRuleCatalog(request.CommandId), owner => owner.Acceptance,
                async (owner, token) =>
                {
                    try
                    {
                        var ready = owner.PrepareRevision(request.ExpectedSequence);
                        if (!ready.IsSuccess)
                            return Result<FirstResponseDescriptor>.Failure(ready.Error!);
                        var current = await rules.ReadAsync(owner, token).ConfigureAwait(false);
                        var pure = current.Rules;
                        var decision = pure.ReviseRules(request.CommandId, request.ExpectedSequence, request.Content, actor, operation.AcceptedAt);
                        if (!decision.IsSuccess)
                            return Result<FirstResponseDescriptor>.Failure(decision.Error!);
                        prepared = new(owner.Stream, request.CommandId, pure.Sequence, decision.Value, owner.CurrentContent);
                        body = await content.WriteJsonAsync(IndependenceCatalogContent.Realm, Uuid.CreateVersion4(), IndependenceCatalogContent.RuleSchema,
                            prepared, token).ConfigureAwait(false);
                        var frozen = await IndependenceCatalogContent.FreezeResultAsync(content, IndependenceCatalogContent.RuleResultSchema,
                            decision.Value, token).ConfigureAwait(false);
                        result = frozen.Result;
                        sequence = owner.NativeSequence;
                        position = owner.CommittedStreamPosition;
                        return Result<FirstResponseDescriptor>.Success(frozen.Response);
                    }
                    catch (ImmutableContentIntegrityException error)
                    {
                        return Result<FirstResponseDescriptor>.Failure(IndependenceCatalogContent.Failure(error));
                    }
                }, (owner, first) => owner.Append(new IndependenceRulesRevised(IndependenceCatalogContent.Header(prepared!), body!, sequence + 1,
                    operation, first, result!), prepared!, sequence, position)), context, ct).ConfigureAwait(false);
            return await IndependenceCatalogContent.ResponseAsync<IndependenceRuleVersionView>(content, accepted,
                IndependenceCatalogContent.RuleResultSchema, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error) { return Result<IndependenceRuleVersionView>.Failure(IndependenceCatalogContent.Failure(error)); }
    }
    static Result<IndependenceRuleVersionView> Failure(RequestErrorKind kind, string message) =>
        Result<IndependenceRuleVersionView>.Failure(new RequestError(kind, message));
}
