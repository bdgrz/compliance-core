using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Services;

public sealed class MethodologyTemplateContributionOperations(NativeOperationExecutor operations,
    IImmutableContentStore content, TimeProvider clock)
{
    public async ValueTask<Result> ExecuteAsync<TRequest>(IRequestContext<TRequest> context, Uuid tenantId,
        Uuid contributionId, Uuid commandId, string kind, bool platform,
        Func<MethodologyTemplateContribution, Uuid, Result> guard,
        Func<MethodologyTemplateContributionRules, Uuid, string, DateTimeOffset, CommandFailure?> prepare,
        Func<MethodologyTemplateContribution, OperationProvenance, Uuid, Uuid, ImmutableContentRef,
            ImmutableContentRef, ImmutableContentRef, Result> apply, CancellationToken ct) where TRequest : IRequestBase
    {
        if (tenantId == Uuid.Empty || contributionId == Uuid.Empty || commandId == Uuid.Empty ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId) || RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A contribution needs its tenant, stable command and personal actor."));
        try
        {
            var actor = MethodologyTemplateCommandActor.For(context, platform ? Uuid.Empty : tenantId);
            var canonical = platform ? ActorReference.ForPlatformOperator(userId, userId.ToString())
                : ActorReference.ForMember(actor.MemberId, actor.MemberId.ToString());
            var owner = new MethodologyTemplateContribution(tenantId, contributionId, commandId);
            var provenance = OwnerProvenance.For(owner, commandId, kind, canonical, contributionId.ToString(), null,
                AuthoredIntentDigest.Compute(context.Request), clock.GetUtcNow());
            ImmutableContentRef? change = null;
            ImmutableContentRef? result = null;
            var accepted = await operations.ExecuteAsync(new NativeOperation<MethodologyTemplateContribution>(
                AcceptedOperationBinding.Of(provenance),
                () => new MethodologyTemplateContribution(tenantId, contributionId, commandId), value => value.Acceptance,
                async (current, token) =>
                {
                    var valid = guard(current, userId);
                    if (!valid.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(valid.Error!);
                    var view = await MethodologyTemplateContributionReader.ReadAsync(current, tenantId, contributionId,
                        content, token).ConfigureAwait(false);
                    var rules = new MethodologyTemplateContributionRules(tenantId, contributionId, view);
                    if (prepare(rules, actor.MemberId, actor.Display, provenance.AcceptedAt) is { } failure)
                        return Result<FirstResponseDescriptor>.Failure(failure.ToRequestError());
                    change = await content.WriteJsonAsync(tenantId.ToString(), Uuid.CreateVersion4(),
                        MethodologyTemplateContributionReader.Schema, rules.Current!, token).ConfigureAwait(false);
                    var body = await content.WriteJsonAsync(tenantId.ToString(), Uuid.CreateVersion4(), UnitResult.Schema,
                        new UnitResult(), token).ConfigureAwait(false);
                    result = await content.WriteJsonAsync(tenantId.ToString(), Uuid.CreateVersion4(),
                        "mutation-result-content.v1", new MutationResultContent(UnitResult.Schema,
                            new StandardResponseBody(body)), token).ConfigureAwait(false);
                    return Result<FirstResponseDescriptor>.Success(new FirstResponseDescriptor(200,
                        ImmutableContentStoreExtensions.JsonMediaType, UnitResult.Schema, new StandardResponseBody(body)));
                },
                (current, frozen) => apply(current, provenance, actor.MemberId, userId, change!, frozen, result!)), context, ct)
                .ConfigureAwait(false);
            var outcome = accepted.IsSuccess ? accepted.Value : null;
            if (accepted.IsSuccess && outcome is AcceptedAppendResult.Committed or AcceptedAppendResult.Replayed)
            {
                var descriptor = outcome switch
                {
                    AcceptedAppendResult.Committed done => done.Response,
                    AcceptedAppendResult.Replayed replay => replay.Response,
                    _ => throw new InvalidOperationException("The accepted contribution has no original result."),
                };
                if (descriptor.ResultSchema != UnitResult.Schema || descriptor.Body is not StandardResponseBody body)
                    throw new ImmutableContentIntegrityException("The contribution operation has another original result.");
                _ = await content.ReadJsonAsync<UnitResult>(tenantId.ToString(), body.Content, ct).ConfigureAwait(false);
            }
            return UnitResponses.ToResult(accepted);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The contribution's immutable content cannot be verified.",
                isTransient: exception is ImmutableContentUnavailableException));
        }
    }
}
