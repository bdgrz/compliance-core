using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Tests.Testing;
/// <summary>Publishes real immutable native evaluation facts for existing business source fixtures.</summary>
static class RiskEvaluationSeeds
{
    public static async ValueTask<RiskEvaluation> WriteAsync(IAggregateWriter writer, IImmutableContentStore content,
        RiskEvaluationRules source, CancellationToken ct = default)
    {
        var owner = new RiskEvaluation(source.TenantId, source.Id);
        var rules = new RiskEvaluationRules(source.TenantId, source.Id);
        await ApplyAsync(owner, rules, source.AppliedChanges, content, ct);
        await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System), ct);
        source.ClearPendingChanges();
        source.SourceNativeSequence = owner.NativeSequence;
        source.SourcePhysicalPosition = owner.CommittedStreamPosition;
        return owner;
    }
    public static async ValueTask<RiskEvaluation> AppendAsync(IAggregateReader reader, IAggregateWriter writer, IDomainEventReader events,
        IImmutableContentStore content, RiskEvaluationRules source, CancellationToken ct = default)
    {
        var snapshot = await new RiskEvaluationReader(reader, events, content).ReadAsync(source.TenantId, source.ProgramId, source.Id, ct);
        await ApplyAsync(snapshot.Owner, snapshot.Rules, source.PendingChanges.ToArray(), content, ct);
        await writer.SaveAsync(snapshot.Owner, new RequestDispatchContext(RequestActor.System), ct);
        source.ClearPendingChanges();
        source.SourceNativeSequence = snapshot.Owner.NativeSequence;
        source.SourcePhysicalPosition = snapshot.Owner.CommittedStreamPosition;
        return snapshot.Owner;
    }
    public static async ValueTask<RiskEvaluation> OwnerAsync(IImmutableContentStore content, RiskEvaluationRules source,
        CancellationToken ct = default)
    {
        var owner = new RiskEvaluation(source.TenantId, source.Id);
        await ApplyAsync(owner, new RiskEvaluationRules(source.TenantId, source.Id), source.AppliedChanges, content, ct);
        return owner;
    }
    static async ValueTask ApplyAsync(RiskEvaluation owner, RiskEvaluationRules rules, IReadOnlyList<object> changes,
        IImmutableContentStore content, CancellationToken ct)
    {
        foreach (var change in changes)
        {
            var before = rules.ToView();
            rules.ClearPendingChanges();
            rules.RaiseEvent(change);
            var kind = change switch
            {
                RiskAssessmentRecordedBody => nameof(RecordRiskAssessment),
                RiskTreatmentChosenBody => nameof(ChooseRiskTreatment),
                _ => nameof(AcceptRisk),
            };
            var digest = change switch
            {
                RiskAssessmentRecordedBody value => value.AuthoredIntentSha256!,
                RiskTreatmentChosenBody value => value.AuthoredIntentSha256!,
                RiskAcceptedBody value => value.AuthoredIntentSha256!,
                _ => throw new InvalidOperationException("Another evaluation fixture mutation kind."),
            };
            var operation = OwnerProvenance.For(owner, RiskEvaluationContent.LogicalId(change), kind, RiskEvaluationContent.Actor(change),
                owner.Id.ToString(), unchecked((ulong)before.Revision), digest, RiskEvaluationContent.At(change)) with
            { FirstOwner = owner.Stream };
            var frozen = await RiskEvaluationContent.FreezeAsync(content, before, rules, kind, ct);
            var response = change switch
            {
                RiskAssessmentRecordedBody value => await NativeContent.FreezeAsync(content, owner.TenantId, RiskEvaluationContent.AssessmentResultSchema, value.Assessment, ct),
                RiskAcceptedBody value => await NativeContent.FreezeAsync(content, owner.TenantId, RiskEvaluationContent.AcceptanceResultSchema, value.Acceptance, ct),
                _ => await NativeContent.FreezeUnitAsync(content, owner.TenantId, ct),
            };
            var first = await FirstResponses.FreezeAsync(content, owner.TenantId.ToString(), response.Response, ct);
            var result = owner.Append(RiskEvaluationContent.Fact(frozen, owner.NativeSequence + 1, operation, first, response.Result),
                frozen, owner.NativeSequence, owner.CommittedStreamPosition);
            Assert.True(result.IsSuccess, result.Error?.Message);
        }
    }
}
