using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Records the record compliance incident intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.incidents.record-compliance-incident", 2)]
public sealed record RecordComplianceIncident(Uuid TenantId, Uuid ProgramId, Uuid IncidentId, ComplianceIncidentContent Content) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
