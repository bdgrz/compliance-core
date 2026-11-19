using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Queries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.App.Features.Criteria;

/// <summary>Declares the operator-facing criteria authoring MCP surface.</summary>
/// <remarks>
/// The API host registers these tools with the criteria authoring handlers and authorization.
/// Independently retried participant work requires an active worker host.
/// </remarks>
public static class CriteriaAuthoringMcpExtensions
{
    /// <summary>Adds typed criteria authoring and operator-receipt tools.</summary>
    public static PortiaBuilder AddCriteriaAuthoringMcpTools(this PortiaBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .AddMcpTool<DraftCriteriaEdition>()
            .AddMcpTool<ChooseInitialEditionLabel>()
            .AddMcpTool<CancelCriteriaEditionDraftRequest>(tool => tool.Destructive())
            .AddMcpTool<BeginCriteriaEditionRename>()
            .AddMcpTool<CancelCriteriaEditionRename>(tool => tool.Destructive())
            .AddMcpTool<AbandonCriteriaEditionDraft>(tool => tool.Destructive())
            .AddMcpTool<ProposeCriterionChange>(tool => tool.Destructive())
            .AddMcpTool<CorrectCriterionSummary>()
            .AddMcpTool<BeginCriteriaPublication>()
            .AddMcpTool<SetCriteriaEditionField>()
            .AddMcpTool<SetCriteriaEditionDisclosure>()
            .AddMcpTool<GetCriteriaEditionDraft>(tool => tool.ReadOnly())
            .AddMcpTool<GetCriteriaOperation>(tool => tool.ReadOnly())
            .AddMcpTool<GetCriteriaEditionValueOperation>(tool => tool.ReadOnly());
    }
}
