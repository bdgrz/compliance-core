using Cntryl.Portia;
using Cntryl.Fitz.Extensions;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Incidents.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Incidents.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.incidents.list-compliance-incidents", 1)]
public sealed record ListComplianceIncidents(Uuid TenantId, Uuid ProgramId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<ComplianceIncidentSummaryView>>, IProgramReadRequest, ICallable;
