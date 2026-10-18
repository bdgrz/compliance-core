using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Records the assess compliance incident intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.incidents.assess-compliance-incident", 2)]
public sealed record AssessComplianceIncident(Uuid TenantId, Uuid ProgramId, Uuid IncidentId, long ExpectedRevision, ComplianceIncidentImpactAssessmentContent Content) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
