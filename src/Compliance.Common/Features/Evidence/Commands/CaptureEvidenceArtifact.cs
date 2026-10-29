using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Evidence.Commands;

/// <summary>Uploads and registers one evidence artifact for governed inspection and storage.</summary>
[Discriminator("bdgrz.evidence.artifact.capture", 1)]
public sealed record CaptureEvidenceArtifact(Uuid TenantId, byte[] Content, EvidenceArtifactContent Metadata)
    : IRequest<EvidenceCapture>, IClientManagementMutationRequest, ICallable;
