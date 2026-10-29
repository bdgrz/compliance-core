using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.ControlMappings.Rules;
using Bdgrz.Compliance.Features.ControlMappings.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.ControlMappings.Aggregates;
using Bdgrz.Compliance.Features.ControlMappings.Commands;
using Bdgrz.Compliance.Features.ControlMappings.Events;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;

public sealed class ApplicabilityNativeActorBindingTests
{
    [Theory]
    [InlineData("different_member", false)]
    [InlineData("different_member", true)]
    [InlineData("non_member", false)]
    [InlineData("non_member", true)]
    [InlineData("malformed_member", false)]
    [InlineData("malformed_member", true)]
    public async Task ShouldRejectForgedProposerAuthorityGivenCoherentNativeAndImmutableHeaders(string malformed,
        bool execute)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var edition = Uuid.CreateVersion4();
        var proposal = Uuid.CreateVersion4();
        var member = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(member, "Personal proposer");
        var rules = new CriterionApplicabilityRules(tenant, program, edition, "CC6.1");
        Assert.True(rules.Propose(MappingRuleFacts.Root(rules, proposal, nameof(ProposeCriterionNotApplicable), 0, member),
            "The criterion is outside scope.", member, actor, NativeFacts.Now).IsSuccess);
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        var owner = await MappingNativeSeeds.BuildAsync(rules, content);
        var original = Assert.IsType<CriterionNotApplicableProposed>(Assert.Single(NativeFacts.Raised(owner)));
        var claimedMember = malformed == "different_member" ? Uuid.CreateVersion4() : member;
        var claimedActor = malformed switch
        {
            "non_member" => new ActorReference("service", member.ToString(), "Service identity"),
            "malformed_member" => new ActorReference("member", "not-a-member-uuid", "Malformed identity"),
            _ => actor,
        };
        var operation = original.Operation with { Actor = claimedActor };
        var body = await NativeContent.ReadAsync<MappingLineMutation>(content, tenant, original.Content);
        var decoded = await MappingLineContent.ReadAsync(content, original, CancellationToken.None);
        var change = Assert.IsType<CriterionNotApplicableProposedChange>(decoded.Change) with
        { ProposerMemberId = claimedMember, Actor = claimedActor, Operation = operation };
        var current = body.Current.Deserialize(ComplianceCoreJsonContext.Default.CriterionApplicabilityView)!;
        var alteredCurrent = current with
        { Versions = [current.Versions[0] with { ProposedBy = claimedActor }] };
        var qualifiedBody = await NativeContent.WriteAsync(content, tenant, MappingLineContent.Schema,
            body with { Change = MappingLineContent.Json(change), Current = MappingLineContent.Json(alteredCurrent) });
        var forged = original with
        {
            ProposerMemberId = claimedMember,
            Actor = claimedActor,
            Operation = operation,
            Content = qualifiedBody,
            Header = original.Header with
            { Pending = original.Header.Pending! with { ProposerMemberId = claimedMember } },
        };
        forged.AttachMetadata(original.Metadata);
        var events = new InMemoryEventStore();
        await events.AppendAsync(owner.Stream, 0, [forged]);
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddPortia();
        services.AddVerifiedMappingLineReader();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var entered = false;

        // Act
        var failure = await Record.ExceptionAsync(async () =>
        {
            var fresh = new CriterionApplicability(tenant, program, edition, "CC6.1");
            if (execute)
                await scope.ServiceProvider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(fresh, _ =>
                { entered = true; return AggregateOutcome.Discard(Result.Success); }, new RequestDispatchContext(RequestActor.System));
            else
                await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(fresh);
        });

        // Assert
        Assert.Equal(original.Metadata, forged.Metadata);
        Assert.Equal(claimedMember, forged.Header.Pending!.ProposerMemberId);
        Assert.Equal(forged.Actor, forged.Operation.Actor);
        Assert.NotEqual(original.Content, qualifiedBody);
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.False(entered);
        Assert.Equal(1, await NativeFacts.CountAsync(scope.ServiceProvider, tenant, CriterionApplicability.Area));
    }
}
