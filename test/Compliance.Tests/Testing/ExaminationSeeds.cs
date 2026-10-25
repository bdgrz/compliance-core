using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Explicit native fixture construction; no runtime legacy read/write path.</summary>
static class ExaminationSeeds
{
    public static async Task WriteAsync(IAggregateReader reader, IAggregateWriter writer, IDomainEventReader events,
        IImmutableContentStore content, ExaminationEngagementRules rules, CancellationToken ct = default)
    {
        foreach (var source in rules.PendingChanges)
        {
            var view = source.Engagement;
            var baseline = source switch
            {
                ExaminationEngagementRuleFrozen frozen => frozen.Baseline,
                ExaminationEngagementRuleAmended amended => amended.Baseline,
                _ => null
            };
            var kind = Kind(source);
            var logical = baseline?.TypeIBaselineId ?? (view.Revision == 1 ? rules.Id :
                Uuid.CreateVersion5(rules.Id, $"seed-revision:{view.Revision}"));
            var owner = await reader.HydrateAsync(new ExaminationEngagement(view.TenantId, view.ProgramId,
                rules.Id, logical, kind), ct);
            _ = await ExaminationEngagementHistory.ReadAsync(events, owner, ct);
            if (owner.Revision >= view.Revision)
                continue;
            Assert.Equal(view.Revision - 1, owner.Revision);
            var operation = new OperationProvenance(logical, logical, null, kind,
                ActorReference.ForMember(view.ActorMemberId, view.ActorDisplay), owner.Stream, RootOperation.InitialEpisode,
                view.Revision == 1 ? null : (ulong)(view.Revision - 1), Intent(source, logical), source.OccurredAt,
                owner.Stream, owner.Stream, owner.Stream);
            var state = new ExaminationEngagementState(view, baseline);
            var change = await content.WriteJsonAsync(view.TenantId.ToString(), Uuid.CreateVersion4(),
                ExaminationEngagementState.Schema, state, ct);
            var unit = await content.WriteJsonAsync(view.TenantId.ToString(), Uuid.CreateVersion4(), UnitResult.Schema,
                new UnitResult(), ct);
            var response = await FirstResponses.FreezeAsync(content, view.TenantId.ToString(), new FirstResponseDescriptor(200,
                ImmutableContentStoreExtensions.JsonMediaType, UnitResult.Schema, new StandardResponseBody(unit)), ct);
            var result = await content.WriteJsonAsync(view.TenantId.ToString(), Uuid.CreateVersion4(), "mutation-result-content.v1",
                new MutationResultContent(UnitResult.Schema, new StandardResponseBody(unit)), ct);
            Assert.True(owner.Accept(operation, state, change, response, result).IsSuccess);
            await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System), ct);
        }
    }

    public static void Configure(IServiceCollection services, IDomainEventReader events)
    {
        var original = services.Last(item => !item.IsKeyedService && item.ServiceType == typeof(IAggregateReader));
        var key = new object();
        services.Remove(original);
        if (original.ImplementationInstance is { } instance)
            services.Add(ServiceDescriptor.KeyedSingleton(typeof(IAggregateReader), key, instance));
        else if (original.ImplementationFactory is { } factory)
            services.Add(ServiceDescriptor.DescribeKeyed(typeof(IAggregateReader), key,
                (provider, _) => factory(provider), original.Lifetime));
        else
            services.Add(ServiceDescriptor.DescribeKeyed(typeof(IAggregateReader), key,
                original.ImplementationType!, original.Lifetime));
        services.Add(ServiceDescriptor.Describe(typeof(IAggregateReader), provider =>
            new ExaminationEngagementAggregateReader(provider.GetRequiredKeyedService<IAggregateReader>(key), events),
            original.Lifetime));
    }

    static string Kind(ExaminationEngagementRuleChange source) => source switch
    {
        ExaminationEngagementRuleOpened => nameof(CreateExaminationEngagement),
        ExaminationEngagementRuleRevised => nameof(ReviseExaminationEngagement),
        ExaminationEngagementRuleFrozen => nameof(FreezeTypeIBaseline),
        ExaminationEngagementRuleAmended => nameof(AmendTypeIBaseline),
        _ => throw new InvalidOperationException("Unsupported fixture rules change.")
    };
    static string Intent(ExaminationEngagementRuleChange source, Uuid logical)
    {
        var view = source.Engagement;
        return source switch
        {
            ExaminationEngagementRuleOpened => AuthoredIntentDigest.Compute(new CreateExaminationEngagement(view.TenantId,
                view.ProgramId, view.ExaminationEngagementId, view.EngagementType, view.AuditFirm, view.PeriodStart,
                view.PeriodEnd, view.TrustServicesCategories, view.BoundaryId, view.ManagementContactMemberIds)),
            ExaminationEngagementRuleRevised => AuthoredIntentDigest.Compute(new ReviseExaminationEngagement(logical,
                view.TenantId, view.ProgramId, view.ExaminationEngagementId, view.Revision - 1, view.AuditFirm,
                view.PeriodStart, view.PeriodEnd, view.TrustServicesCategories, view.ManagementContactMemberIds)),
            ExaminationEngagementRuleFrozen frozen => AuthoredIntentDigest.Compute(new FreezeTypeIBaseline(view.TenantId,
                view.ProgramId, view.ExaminationEngagementId, frozen.Baseline.TypeIBaselineId, view.Revision - 1,
                frozen.Baseline.ConfirmationRationale, frozen.Baseline.ContentSha256)),
            ExaminationEngagementRuleAmended amended => AuthoredIntentDigest.Compute(new AmendTypeIBaseline(view.TenantId,
                view.ProgramId, view.ExaminationEngagementId, amended.Baseline.TypeIBaselineId, view.Revision - 1,
                amended.Baseline.AmendmentReason!, amended.Baseline.ChangedRecordIds, amended.Baseline.ContentSha256,
                amended.Baseline.AffectedPackageMaterialIds)),
            _ => throw new InvalidOperationException("Unsupported fixture rules change.")
        };
    }
}
