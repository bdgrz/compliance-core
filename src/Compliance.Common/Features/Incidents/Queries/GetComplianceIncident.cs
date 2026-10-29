using Cntryl.Portia;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Incidents.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Incidents.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.incidents.get-compliance-incident", 2)]
public sealed record GetComplianceIncident(Uuid TenantId, Uuid ProgramId, Uuid IncidentId)
    : IRequest<ComplianceIncidentView>, IProgramReadRequest, ICallable;
