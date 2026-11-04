using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;

namespace Bdgrz.Compliance.Features.Evidence.Commands;

/// <summary>Changes an artifact's own current handling class with a reason.</summary>
[Discriminator("bdgrz.evidence.artifact.handling-class.change", 1)]
public sealed record ChangeArtifactHandlingClass(Uuid CommandId, Uuid TenantId, Uuid ArtifactId,
    long ExpectedRevision, string HandlingClass, string Rationale)
    : IRequest, IClientManagementMutationRequest, ICallable;
