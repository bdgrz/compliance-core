using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
namespace Bdgrz.Compliance.Features.Applications.Commands;

/// <summary>Records the cancel readiness material import intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.imports.cancel-readiness-material-import", 1)]
public sealed record CancelReadinessMaterialImport(Uuid TenantId, Uuid ImportId, string Rationale) : IRequest, IClientManagementMutationRequest, ICallable;
