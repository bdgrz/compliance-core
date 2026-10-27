using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AccessControl.Contracts;

/// <summary>Marks change and incident mutations that must honor restricted-record editing rules.</summary>
public interface IRestrictedComplianceRecordMutationRequest :
    IRequestBase, IProgramScopedRequest, IClientManagementMutationRequest
{
    new Uuid TenantId { get; }
}
