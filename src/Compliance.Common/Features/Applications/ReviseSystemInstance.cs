using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Records the revise system instance intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.applications.revise-system-instance", 1)]
public sealed record ReviseSystemInstance(Uuid TenantId, Uuid ApplicationId, Uuid SystemInstanceId, long ExpectedRevision, string Name, string SourceIdentifier, string AccessBoundary, string Rationale) : IRequest, IApplicationInventoryWriteRequest, ICallable;
