using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Records an exact, trusted-worker result for a persisted artifact disposition attempt.</summary>
[Discriminator("bdgrz.retention.record-artifact-disposition-result", 2)]
public sealed record RecordArtifactDispositionResult(Uuid TenantId, Uuid DispositionId,
    Uuid ArtifactId, long ExpectedRevision, int AttemptNumber, Uuid ExecutionRequestEventId,
    string Outcome, string? FailureCode, DateTimeOffset CompletedAt) : IRequest;
