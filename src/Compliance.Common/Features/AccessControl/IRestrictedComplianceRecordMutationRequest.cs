using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Marks change and incident mutations that must honor restricted-record editing rules.</summary>
public interface IRestrictedComplianceRecordMutationRequest :
    IRequestBase, IProgramScopedRequest, IClientManagementMutationRequest
{
    new Uuid TenantId { get; }
}
