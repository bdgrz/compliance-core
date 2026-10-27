using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Rules;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
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
