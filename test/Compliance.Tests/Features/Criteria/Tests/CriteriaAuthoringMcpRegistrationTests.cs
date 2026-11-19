using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Queries;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaAuthoringMcpRegistrationTests
{
    [Fact]
    public void ShouldDeclareExactTypedOperatorToolsGivenExplicitRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddPortia();
        var expected = new Dictionary<Type, (string Name, bool ReadOnly, bool Destructive)>
        {
            [typeof(DraftCriteriaEdition)] = ("bdgrz.criteria.edition.draft", false, false),
            [typeof(ChooseInitialEditionLabel)] = ("bdgrz.criteria.edition.initial-label.choose", false, false),
            [typeof(CancelCriteriaEditionDraftRequest)] = ("bdgrz.criteria.edition.draft.cancel", false, true),
            [typeof(BeginCriteriaEditionRename)] = ("bdgrz.criteria.edition.rename.begin", false, false),
            [typeof(CancelCriteriaEditionRename)] = ("bdgrz.criteria.edition.rename.cancel", false, true),
            [typeof(AbandonCriteriaEditionDraft)] = ("bdgrz.criteria.edition.draft.abandon", false, true),
            [typeof(ProposeCriterionChange)] = ("bdgrz.criteria.entry.change.propose", false, true),
            [typeof(CorrectCriterionSummary)] = ("bdgrz.criteria.entry.summary.correct", false, false),
            [typeof(BeginCriteriaPublication)] = ("bdgrz.criteria.edition.publication.begin", false, false),
            [typeof(SetCriteriaEditionField)] = ("bdgrz.criteria.edition-field.set", false, false),
            [typeof(SetCriteriaEditionDisclosure)] = ("bdgrz.criteria.edition-disclosure.set", false, false),
            [typeof(GetCriteriaEditionDraft)] = ("bdgrz.criteria.edition.draft.get", true, false),
            [typeof(GetCriteriaOperation)] = ("bdgrz.criteria.operation.get", true, false),
            [typeof(GetCriteriaEditionValueOperation)] = ("bdgrz.criteria.edition-value.operation.get", true, false),
        };

        // Act
        var returned = builder.AddCriteriaAuthoringMcpTools();
        using var provider = services.BuildServiceProvider();
        var tools = provider.GetServices<McpToolRegistration>().ToArray();

        // Assert
        Assert.Same(builder, returned);
        Assert.Equal(expected.Count, tools.Length);
        foreach (var tool in tools)
        {
            var definition = expected[tool.RequestType];
            Assert.Equal(definition.Name, tool.Name);
            Assert.True(typeof(ICallable).IsAssignableFrom(tool.RequestType));
            Assert.True(typeof(IPlatformOperatorRequest).IsAssignableFrom(tool.RequestType));
            Assert.Equal(definition.ReadOnly, tool.ReadOnly == true);
            Assert.Equal(definition.Destructive, tool.Destructive == true);
            Assert.NotEqual(true, tool.Idempotent);
            Assert.Equal(tool.RequestType == typeof(GetCriteriaEditionDraft)
                ? typeof(CriteriaEditionDraftView) : typeof(CriteriaOperationReceipt), tool.ResultType);
        }
    }

    [Fact]
    public void ShouldNotExposeCriteriaAuthoringToolsGivenRegistrationIsNotExplicit()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services.AddPortia();

        // Act
        using var provider = services.BuildServiceProvider();
        var tools = provider.GetServices<McpToolRegistration>();

        // Assert
        Assert.Empty(tools);
    }
}
