using Cntryl.Portia;
using Cntryl.Fitz.Extensions;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Operations.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.operations.list-observation-periods", 1)]
public sealed record ListObservationPeriods(Uuid TenantId, Uuid ProgramId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<ObservationPeriodSummaryView>>, IProgramReadRequest, ICallable;
