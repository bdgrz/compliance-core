using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.ControlMappings.Aggregates;
using Bdgrz.Compliance.Features.ControlMappings.Commands;
using System.Collections;
using System.Reflection;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.ControlMappings;

public sealed class ControlMappingNativeBoundaryTests
{
    [Fact]
    public async Task ShouldRetainOnlyCurrentAuthorityGivenManyMappingAndApplicabilityReviews()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var control = Uuid.CreateVersion4();
        var edition = Uuid.CreateVersion4();
        var author = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        var mappingRules = new ControlCriterionMappingRules(tenant, program, control, edition, "CC6.1");
        var applicabilityRules = new CriterionApplicabilityRules(tenant, program, edition, "CC6.1");
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());

        // Act
        for (var index = 0; index < 105; index++)
        {
            var proposal = Uuid.CreateVersion4();
            Assert.True(mappingRules.Propose(MappingRuleFacts.Root(mappingRules, proposal, nameof(ProposeControlCriterionMapping),
                mappingRules.Revision, author), Uuid.CreateVersion4(), "criterion", "Exact reason " + index,
                "All scoped systems", author, "Author", NativeFacts.Now).IsSuccess);
            Assert.True(mappingRules.Review(MappingRuleFacts.Root(mappingRules, Uuid.CreateVersion4(), nameof(ReviewControlCriterionMapping),
                mappingRules.Revision, reviewer), proposal, "reject", "Reconsider the mapping.", reviewer, "Reviewer",
                NativeFacts.Now, null).IsSuccess);
            var decision = Uuid.CreateVersion4();
            Assert.True(applicabilityRules.Propose(MappingRuleFacts.Root(applicabilityRules, decision, nameof(ProposeCriterionNotApplicable),
                applicabilityRules.Revision, author), "Not applicable " + index, author,
                ActorReference.ForMember(author, "Author"), NativeFacts.Now).IsSuccess);
            Assert.True(applicabilityRules.Review(MappingRuleFacts.Root(applicabilityRules, Uuid.CreateVersion4(), nameof(ReviewCriterionApplicability),
                applicabilityRules.Revision, reviewer), decision, "reject", "Coverage remains required.", reviewer,
                ActorReference.ForMember(reviewer, "Reviewer"), NativeFacts.Now, null).IsSuccess);
        }
        var mapping = await MappingNativeSeeds.BuildAsync(mappingRules, content);
        var applicability = await MappingNativeSeeds.BuildAsync(applicabilityRules, content);
        var retained = new Aggregate[] { mapping, applicability }.SelectMany(owner => owner.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(field => field.GetValue(owner) is ICollection { Count: > 2 })).ToArray();

        // Assert
        Assert.Equal(210, mapping.Revision);
        Assert.Equal(210, applicability.Revision);
        Assert.Empty(retained);
        Assert.Equal(105, mapping.Header.VersionCount);
        Assert.Equal(105, applicability.Header.VersionCount);
        Assert.Null(mapping.Header.Pending);
        Assert.Null(applicability.Header.Pending);
    }

    [Fact]
    public async Task ShouldBoundNativeFactGivenMaximumValidEscapedMappingTexts()
    {
        // Arrange
        var rules = new ControlCriterionMappingRules(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), "CC6.1");
        var id = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var text = new string('界', ControlCriterionMapping.MaximumTextLength);
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());

        // Act
        var outcome = rules.Propose(MappingRuleFacts.Root(rules, id, nameof(ProposeControlCriterionMapping), 0, actor),
            Uuid.CreateVersion4(), "criterion", text, text, actor, "Author", NativeFacts.Now);
        var mapping = await MappingNativeSeeds.BuildAsync(rules, content);
        var fact = Assert.Single(new AggregateScenario<ControlCriterionMapping>(mapping).PendingEvents);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(fact, ComplianceCoreJsonContext.Default.GetTypeInfo(fact.GetType())!);
        var decoded = await MappingLineContent.ReadAsync(content, fact, CancellationToken.None);
        var change = Assert.IsType<ControlCriterionMappingProposedChange>(decoded.Change);

        // Assert
        Assert.True(outcome.IsSuccess, outcome.Error?.Message);
        Assert.True(bytes.Length <= 48 * 1024, $"The complete valid mapping produced a {bytes.Length}-byte native payload.");
        Assert.Equal(text, change.Rationale);
        Assert.Equal(text, change.ApplicabilityExplanation);
        Assert.Equal(1, mapping.Revision);
    }
}
