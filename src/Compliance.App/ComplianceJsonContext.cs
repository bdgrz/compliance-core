using System.Text.Json;
using System.Text.Json.Serialization;
using Cntryl.Portia;
using Microsoft.AspNetCore.Mvc;
using Bdgrz.Compliance.Features.Authentication;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Responsibilities.Commands;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Responsibilities.Queries;
using Bdgrz.Compliance.Features.SystemDescription.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance;

[PortiaJsonContext]
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(ComplianceAuthenticationClientConfiguration))]
[JsonSerializable(typeof(ApplicationChangePreview))]
[JsonSerializable(typeof(ApplicationVendorReferenceView))]
[JsonSerializable(typeof(ApplicationControlVersionReferenceView))]
[JsonSerializable(typeof(ApplicationPolicyReferenceView))]
[JsonSerializable(typeof(ApplicationEvidenceSourceReferenceView))]
[JsonSerializable(typeof(ApplicationAccessPopulationReferenceView))]
[JsonSerializable(typeof(ApplicationReviewCampaignReferenceView))]
[JsonSerializable(typeof(ReadinessAssessmentView))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(int?))]
[JsonSerializable(typeof(DateOnly))]
[JsonSerializable(typeof(Uuid))]
[JsonSerializable(typeof(IReadOnlyList<Uuid>))]
[JsonSerializable(typeof(IReadOnlyList<SystemDescriptionSection>))]
[JsonSerializable(typeof(AssignResponsibility))]
[JsonSerializable(typeof(RatifyIndependenceRuleVersion))]
[JsonSerializable(typeof(IndependenceRuleRatificationView))]
[JsonSerializable(typeof(PartnerIndependenceEvaluationView))]
[JsonSerializable(typeof(RecordFirmProfessionalDutyDesignation))]
[JsonSerializable(typeof(RevokeFirmProfessionalDutyDesignation))]
[JsonSerializable(typeof(GetFirmProfessionalDutyCatalog))]
[JsonSerializable(typeof(FirmProfessionalDutyDesignationView))]
[JsonSerializable(typeof(FirmProfessionalDutyCatalogView))]
[JsonSerializable(typeof(RevokeServiceEngagementActualStaff))]
[JsonSerializable(typeof(RecordApplicationRelationship))]
[JsonSerializable(typeof(RemoveGovernedApplicationRelationship))]
[JsonSerializable(typeof(ListDirectedApplicationRelationships))]
[JsonSerializable(typeof(ApproveApplicationSuccessor))]
[JsonSerializable(typeof(RevokeResponsibility))]
[JsonSerializable(typeof(GrantAccess))]
[JsonSerializable(typeof(RevokeAccessGrant))]
[JsonSerializable(typeof(ListAccessGrants))]
[JsonSerializable(typeof(AccessGrantProposal))]
[JsonSerializable(typeof(AccessGrantPrincipal))]
[JsonSerializable(typeof(AccessGrantScope))]
[JsonSerializable(typeof(AccessGrantSource))]
[JsonSerializable(typeof(AccessGrantSetView))]
[JsonSerializable(typeof(AccessGrantView))]
[JsonSerializable(typeof(ListResponsibilities))]
[JsonSerializable(typeof(PreviewResponsibilityConflicts))]
[JsonSerializable(typeof(ResponsibilitySetView))]
[JsonSerializable(typeof(TenantMembershipView))]
[JsonSerializable(typeof(SuspendMember))]
[JsonSerializable(typeof(ReinstateMember))]
[JsonSerializable(typeof(DeprovisionMember))]
[JsonSerializable(typeof(GetTenantMember))]
[JsonSerializable(typeof(ListMemberResponsibilities))]
[JsonSerializable(typeof(IReadOnlyList<ResponsibilityAssignmentView>))]
[JsonSerializable(typeof(ResponsibilityConflictPreview))]
[JsonSerializable(typeof(BrowserSession))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(PersonalContactDetails))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.CriteriaTextOverlayContent))]
[JsonSerializable(typeof(EvidenceArtifactContent))]
[JsonSerializable(typeof(EvidenceCapture))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Work.WorkDigestRetryHttpBody))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Work.WorkAssignmentHttp.ClaimBody))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Work.WorkAssignmentHttp.AssignBody))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Work.WorkAssignmentHttp.DelegateBody))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Work.WorkAssignmentHttp.EscalateBody))]
[JsonSerializable(typeof(PersonHttp.RecordBody))]
[JsonSerializable(typeof(PersonHttp.ReviseBody))]
[JsonSerializable(typeof(PersonHttp.CorrelateBody))]
sealed partial class ComplianceJsonContext : JsonSerializerContext;
