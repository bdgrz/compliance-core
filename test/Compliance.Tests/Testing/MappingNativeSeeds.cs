using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

static class MappingNativeSeeds
{
    public static async Task AppendAsync(IAggregateReader reader, IAggregateWriter writer,
        IImmutableContentStore content, ControlCriterionMappingRules history)
    {
        var rules = new ControlCriterionMappingRules(history.TenantId, history.ProgramId, history.ControlId, history.EditionId, history.CriterionIdentifier);
        foreach (var original in history.Changes)
        {
            var owner = await reader.HydrateAsync(new ControlCriterionMapping(history.TenantId, history.ProgramId,
                history.ControlId, history.EditionId, history.CriterionIdentifier));
            var rebound = Rebind(original);
            rules.Apply(rebound.Change);
            if (rules.Revision <= owner.Revision)
                continue;
            var (response, result) = rebound.Change is ControlCriterionMappingProposedChange proposed
                ? await NativeContent.FreezeAsync(content, history.TenantId, ControlCriterionMapping.RegistrationSchema,
                    new ControlCriterionMappingRegistration(proposed.MappingId, proposed.Revision, proposed.VersionNumber))
                : await NativeContent.FreezeUnitAsync(content, history.TenantId);
            var prepared = await MappingLineContent.PrepareJsonAsync(content, owner, rebound.Authored, rebound.Change,
                MappingLineContent.Current(rules), MappingLineContent.Header(rules), result, CancellationToken.None);
            var first = await FirstResponses.FreezeAsync(content, history.TenantId.ToString(), response, CancellationToken.None);
            Assert.True(owner.Apply(prepared, first).IsSuccess);
            await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
        }
    }

    public static async Task AppendAsync(IAggregateReader reader, IAggregateWriter writer,
        IImmutableContentStore content, CriterionApplicabilityRules history)
    {
        var rules = new CriterionApplicabilityRules(history.TenantId, history.ProgramId, history.EditionId, history.CriterionIdentifier);
        foreach (var original in history.Changes)
        {
            var owner = await reader.HydrateAsync(new CriterionApplicability(history.TenantId, history.ProgramId,
                history.EditionId, history.CriterionIdentifier));
            var rebound = Rebind(original);
            rules.Apply(rebound.Change);
            if (rules.Revision <= owner.Revision)
                continue;
            var (response, result) = rebound.Change is CriterionNotApplicableProposedChange proposed
                ? await NativeContent.FreezeAsync(content, history.TenantId, CriterionApplicability.RegistrationSchema,
                    new CriterionApplicabilityRegistration(proposed.DecisionId, proposed.Revision, proposed.VersionNumber))
                : await NativeContent.FreezeUnitAsync(content, history.TenantId);
            var prepared = await MappingLineContent.PrepareJsonAsync(content, owner, rebound.Authored, rebound.Change,
                MappingLineContent.Current(rules), MappingLineContent.Header(rules), result, CancellationToken.None);
            var first = await FirstResponses.FreezeAsync(content, history.TenantId.ToString(), response, CancellationToken.None);
            Assert.True(owner.Apply(prepared, first).IsSuccess);
            await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
        }
    }

    public static async Task<ControlCriterionMapping> BuildAsync(ControlCriterionMappingRules history, IImmutableContentStore content)
    {
        var owner = new ControlCriterionMapping(history.TenantId, history.ProgramId, history.ControlId, history.EditionId, history.CriterionIdentifier);
        var rules = new ControlCriterionMappingRules(history.TenantId, history.ProgramId, history.ControlId, history.EditionId, history.CriterionIdentifier);
        foreach (var original in history.Changes)
        {
            var rebound = Rebind(original);
            rules.Apply(rebound.Change);
            var (response, result) = rebound.Change is ControlCriterionMappingProposedChange proposed
                ? await NativeContent.FreezeAsync(content, history.TenantId, ControlCriterionMapping.RegistrationSchema,
                    new ControlCriterionMappingRegistration(proposed.MappingId, proposed.Revision, proposed.VersionNumber))
                : await NativeContent.FreezeUnitAsync(content, history.TenantId);
            var prepared = await MappingLineContent.PrepareJsonAsync(content, owner, rebound.Authored, rebound.Change,
                MappingLineContent.Current(rules), MappingLineContent.Header(rules), result, CancellationToken.None);
            var first = await FirstResponses.FreezeAsync(content, history.TenantId.ToString(), response, CancellationToken.None);
            Assert.True(owner.Apply(prepared, first).IsSuccess);
        }
        return owner;
    }

    public static async Task<CriterionApplicability> BuildAsync(CriterionApplicabilityRules history, IImmutableContentStore content)
    {
        var owner = new CriterionApplicability(history.TenantId, history.ProgramId, history.EditionId, history.CriterionIdentifier);
        var rules = new CriterionApplicabilityRules(history.TenantId, history.ProgramId, history.EditionId, history.CriterionIdentifier);
        foreach (var original in history.Changes)
        {
            var rebound = Rebind(original);
            rules.Apply(rebound.Change);
            var (response, result) = rebound.Change is CriterionNotApplicableProposedChange proposed
                ? await NativeContent.FreezeAsync(content, history.TenantId, CriterionApplicability.RegistrationSchema,
                    new CriterionApplicabilityRegistration(proposed.DecisionId, proposed.Revision, proposed.VersionNumber))
                : await NativeContent.FreezeUnitAsync(content, history.TenantId);
            var prepared = await MappingLineContent.PrepareJsonAsync(content, owner, rebound.Authored, rebound.Change,
                MappingLineContent.Current(rules), MappingLineContent.Header(rules), result, CancellationToken.None);
            var first = await FirstResponses.FreezeAsync(content, history.TenantId.ToString(), response, CancellationToken.None);
            Assert.True(owner.Apply(prepared, first).IsSuccess);
        }
        return owner;
    }

    public static async Task<DomainEvent[]> RaisedAsync(ControlCriterionMappingRules history, IImmutableContentStore content) =>
        NativeFacts.Raised(await BuildAsync(history, content));

    public static (object Change, JsonElement Authored) Rebind(object original) => original switch
    {
        ControlCriterionMappingProposedChange value => Bind(value, new ProposeControlCriterionMapping(value.MappingId, value.TenantId, value.ProgramId, value.ControlId, value.ControlVersionId, value.EditionId, value.CriterionIdentifier, (long)value.Operation.OriginalExpectedRevision!, value.Rationale, value.ApplicabilityExplanation)),
        ControlCriterionMappingReviewedChange value => Bind(value, new ReviewControlCriterionMapping(value.DecisionId, value.TenantId, value.ProgramId, value.MappingId, (long)value.Operation.OriginalExpectedRevision!, value.Outcome, value.Rationale, value.SeparationOfDutiesWaiverId)),
        ControlCriterionMappingRetiredChange value => Bind(value, new RetireControlCriterionMapping(value.Operation.LogicalId, value.TenantId, value.ProgramId, value.MappingId, (long)value.Operation.OriginalExpectedRevision!, value.Rationale)),
        CriterionNotApplicableProposedChange value => Bind(value, new ProposeCriterionNotApplicable(value.DecisionId, value.TenantId, value.ProgramId, value.EditionId, value.CriterionIdentifier, (long)value.Operation.OriginalExpectedRevision!, value.Rationale)),
        CriterionApplicabilityReviewedChange value => Bind(value, new ReviewCriterionApplicability(value.ReviewDecisionId, value.TenantId, value.ProgramId, value.DecisionId, (long)value.Operation.OriginalExpectedRevision!, value.Outcome, value.Rationale, value.SeparationOfDutiesWaiverId)),
        CriterionNotApplicableWithdrawnChange value => Bind(value, new WithdrawCriterionNotApplicable(value.Operation.LogicalId, value.TenantId, value.ProgramId, value.DecisionId, (long)value.Operation.OriginalExpectedRevision!, value.Rationale)),
        _ => throw new InvalidOperationException("The seed contains another business change."),
    };
    static (object, JsonElement) Bind(ControlCriterionMappingProposedChange value, ProposeControlCriterionMapping request) =>
        (value with { Operation = value.Operation with { AuthoredIntentSha256 = AuthoredIntentDigest.Compute(request), Actor = value.Actor, AcceptedAt = value.ProposedAt } }, MappingLineContent.Json(request));

    static (object, JsonElement) Bind(ControlCriterionMappingReviewedChange value, ReviewControlCriterionMapping request) =>
        (value with { Operation = value.Operation with { AuthoredIntentSha256 = AuthoredIntentDigest.Compute(request), Actor = value.Actor, AcceptedAt = value.DecidedAt } }, MappingLineContent.Json(request));

    static (object, JsonElement) Bind(ControlCriterionMappingRetiredChange value, RetireControlCriterionMapping request) =>
        (value with { Operation = value.Operation with { AuthoredIntentSha256 = AuthoredIntentDigest.Compute(request), Actor = value.Actor, AcceptedAt = value.RetiredAt } }, MappingLineContent.Json(request));

    static (object, JsonElement) Bind(CriterionNotApplicableProposedChange value, ProposeCriterionNotApplicable request) =>
        (value with { Operation = value.Operation with { AuthoredIntentSha256 = AuthoredIntentDigest.Compute(request), Actor = value.Actor, AcceptedAt = value.ProposedAt } }, MappingLineContent.Json(request));

    static (object, JsonElement) Bind(CriterionApplicabilityReviewedChange value, ReviewCriterionApplicability request) =>
        (value with { Operation = value.Operation with { AuthoredIntentSha256 = AuthoredIntentDigest.Compute(request), Actor = value.Actor, AcceptedAt = value.ReviewedAt } }, MappingLineContent.Json(request));

    static (object, JsonElement) Bind(CriterionNotApplicableWithdrawnChange value, WithdrawCriterionNotApplicable request) =>
        (value with { Operation = value.Operation with { AuthoredIntentSha256 = AuthoredIntentDigest.Compute(request), Actor = value.Actor, AcceptedAt = value.WithdrawnAt } }, MappingLineContent.Json(request));

}
