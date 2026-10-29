using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

static class BuiltInRolePermissions
{
    public static IEnumerable<AssignRolePermission> Create(Uuid sourceEventId, Uuid tenantId)
    {
        var administrator = BuiltInRbac.TenantAdministrationRoleId(tenantId);
        var lead = BuiltInRbac.ComplianceManagementRoleId(tenantId);
        var contributor = BuiltInRbac.ComplianceParticipationRoleId(tenantId);
        var viewer = BuiltInRbac.ViewerRoleId(tenantId);
        foreach (var role in new[] { administrator, lead, contributor, viewer })
            yield return RbacPermissionEffects.Assign(sourceEventId, tenantId, role, RbacPermissions.TenantAccess);
        yield return RbacPermissionEffects.Assign(sourceEventId, tenantId, administrator, RbacPermissions.TenantRbacManage);
        string[] managerPermissions =
        [
            RbacPermissions.ProgramManage,
            RbacPermissions.ApplicationInventoryManage,
            RbacPermissions.ApplicationRestrictedRead,
            RbacPermissions.ComplianceRecordRestrictedRead,
            RbacPermissions.ComplianceRecordRestrictedWrite,
            RbacPermissions.TechnologyInventoryManage,
            RbacPermissions.TechnologyInventoryRestrictedRead,
            RbacPermissions.ProviderInventoryManage,
            RbacPermissions.WorkforceManage,
            RbacPermissions.AccessReviewManage,
            FieldClasses.WorkforcePersonalDetails.ReadPermission,
        ];
        foreach (var role in new[] { administrator, lead })
            foreach (var permission in managerPermissions)
                yield return RbacPermissionEffects.Assign(sourceEventId, tenantId, role, permission);
        foreach (var role in new[] { administrator, lead, contributor })
        {
            yield return RbacPermissionEffects.Assign(sourceEventId, tenantId, role, RbacPermissions.ApplicationImportStage);
            yield return RbacPermissionEffects.Assign(sourceEventId, tenantId, role, RbacPermissions.EvidenceRedactionPrepare);
        }
        yield return RbacPermissionEffects.Assign(sourceEventId, tenantId, administrator, FieldClasses.WorkforceManagerChain.ReadPermission);
        yield return RbacPermissionEffects.Assign(sourceEventId, tenantId, lead, RbacPermissions.EvidenceRedactionApprove);
        yield return RbacPermissionEffects.Assign(sourceEventId, tenantId, viewer, RbacPermissions.EvidenceArtifactRead);
    }
}
