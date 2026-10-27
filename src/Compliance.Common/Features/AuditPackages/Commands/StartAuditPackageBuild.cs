using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AuditPackages.Commands;

/// <summary>Internal source-scoped effect; the target verifies the committed source and frozen intent.</summary>
[Discriminator("bdgrz.audit-packages.start-audit-package-build", 1)]
public sealed record StartAuditPackageBuild(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid PackageId, Uuid BuildId, long ManifestVersion)
    : IRequest<ReceiverOutcome>, IAuditPackageReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
