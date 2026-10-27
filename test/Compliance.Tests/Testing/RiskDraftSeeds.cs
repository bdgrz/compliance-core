using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

static class RiskDraftSeeds
{
    public static Task WriteAsync(IAggregateWriter writer, IImmutableContentStore content, RiskDraftRules rules) => SaveAsync(null, writer, content, rules);
    public static Task AppendAsync(IAggregateReader reader, IAggregateWriter writer, IImmutableContentStore content, RiskDraftRules rules) => SaveAsync(reader, writer, content, rules);
    public static async Task<RiskDraft> OwnerAsync(IImmutableContentStore content, RiskDraftRules rules)
    {
        var owner = new RiskDraft(rules.TenantId, rules.Id);
        var current = new RiskDraftRules(rules.TenantId, rules.Id);
        foreach (var change in rules.AppliedChanges)
        {
            current.Apply(change);
            var fact = await FreezeAsync(content, current, change, owner.NativeSequence + 1);
            Assert.True(owner.Append(fact, current.Current!, owner.NativeSequence, owner.CommittedStreamPosition).IsSuccess);
        }
        return owner;
    }
    static async Task SaveAsync(IAggregateReader? reader, IAggregateWriter writer, IImmutableContentStore content, RiskDraftRules source)
    {
        var owner = new RiskDraft(source.TenantId, source.Id);
        if (reader is not null)
            owner = await reader.HydrateAsync(owner);
        var current = owner.CurrentContent is null ? new RiskDraftRules(source.TenantId, source.Id) :
            RiskDraftRules.FromCurrent(await content.ReadJsonAsync<RiskDraftRevisionView>(source.TenantId.ToString(), owner.CurrentContent), source.TenantId, source.Id);
        foreach (var change in (reader is null ? source.AppliedChanges : source.PendingChanges).ToArray())
        {
            current.Apply(change);
            var fact = await FreezeAsync(content, current, change, owner.NativeSequence + 1);
            var outcome = owner.Append(fact, current.Current!, owner.NativeSequence, owner.CommittedStreamPosition);
            Assert.True(outcome.IsSuccess, outcome.Error?.Message);
            await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
        }
        source.ClearPendingChanges();
    }
    public static async Task<DomainEvent> FreezeAsync(IImmutableContentStore content, RiskDraftRules after, object change, long sequence)
    {
        var current = after.Current!;
        var header = RiskDraftFactContent.Header(current);
        var owner = new RiskDraft(after.TenantId, after.Id);
        var created = change is RiskDraftCreatedContent;
        var command = change is RiskDraftRevisedContent { CommandId: { } selected } ? selected :
            created ? after.Id : Uuid.CreateVersion5(after.Id, "fixture:revision:" + current.Revision);
        var digest = change switch
        {
            RiskDraftCreatedContent { AuthoredIntentSha256: { } authored } => authored,
            RiskDraftRevisedContent { AuthoredIntentSha256: { } authored } => authored,
            RiskDraftCreatedContent => AuthoredIntentDigest.Compute(new CreateRiskDraft(after.Id, after.TenantId, current.ProgramId,
                current.Identifier, current.Content.Title, current.Content.Scenario, current.Content.PotentialEffect, current.Content.SourceNote)),
            _ => AuthoredIntentDigest.Compute(new ReviseRiskDraft(command, after.TenantId, current.ProgramId, after.Id,
                current.Revision - 1, current.Content.Title, current.Content.Scenario, current.Content.PotentialEffect, current.Content.SourceNote)),
        };
        var operation = OwnerProvenance.For(owner, command, created ? nameof(CreateRiskDraft) : nameof(ReviseRiskDraft), current.Actor,
            after.Id.ToString(), created ? null : unchecked((ulong)(current.Revision - 1)), digest, current.ChangedAt)
            with
        { FirstOwner = owner.Stream };
        var reference = await NativeContent.WriteAsync(content, after.TenantId, RiskDraftFactContent.Schema, current);
        var response = created ? await NativeContent.FreezeAsync(content, after.TenantId, RiskDraftOperations.RegistrationSchema,
            new RiskRegistration(after.Id, current.Identifier, 1)) : await NativeContent.FreezeUnitAsync(content, after.TenantId);
        var first = await FirstResponses.FreezeAsync(content, after.TenantId.ToString(), response.Response);
        DomainEvent fact = created ? new RiskDraftCreated(header, reference, sequence, operation, first, response.Result) :
            new RiskDraftRevised(header, reference, sequence, operation, first, response.Result);
        return fact;
    }
}
