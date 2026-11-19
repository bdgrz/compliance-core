using Bdgrz.Compliance.Features.Criteria.Handlers;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bdgrz.Compliance.Features.Criteria;

/// <summary>Registers criteria authoring handlers and source-backed receipt readers.</summary>
/// <remarks>
/// The host supplies the aggregate and source services, admission authority, transport and
/// independently retried workers. Operation receipts can remain pending until those workers
/// have resolved the corresponding participant decisions.
/// </remarks>
public static class CriteriaAuthoringRegistration
{
    /// <summary>Registers typed handlers, operations and receipt readers without enabling workers.</summary>
    public static PortiaBuilder AddCriteriaAuthoringServices(this PortiaBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddScoped<CriteriaEditionOperations>();
        builder.Services.TryAddScoped<CriteriaEntryOperations>();
        builder.Services.TryAddScoped<CriteriaEditionValueOperations>();
        builder.Services.TryAddScoped<ICriteriaOperationReader, SourceCriteriaOperationReader>();
        builder.Services.TryAddScoped<ICriteriaEditionValueOperationReader, SourceCriteriaEditionValueOperationReader>();

        return builder
            .AddRequestHandler<DraftCriteriaEditionHandler>()
            .AddRequestHandler<ChooseInitialEditionLabelHandler>()
            .AddRequestHandler<CancelCriteriaEditionDraftRequestHandler>()
            .AddRequestHandler<BeginCriteriaEditionRenameHandler>()
            .AddRequestHandler<CancelCriteriaEditionRenameHandler>()
            .AddRequestHandler<AbandonCriteriaEditionDraftHandler>()
            .AddRequestHandler<BeginCriteriaPublicationHandler>()
            .AddRequestHandler<ProposeCriterionChangeHandler>()
            .AddRequestHandler<CorrectCriterionSummaryHandler>()
            .AddRequestHandler<SetCriteriaEditionFieldHandler>()
            .AddRequestHandler<SetCriteriaEditionDisclosureHandler>()
            .AddRequestHandler<GetCriteriaEditionDraftHandler>()
            .AddRequestHandler<GetCriteriaOperationHandler>()
            .AddRequestHandler<GetCriteriaEditionValueOperationHandler>();
    }
}
