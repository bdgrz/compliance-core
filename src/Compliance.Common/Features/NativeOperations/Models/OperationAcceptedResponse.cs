using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NativeOperations.Models;

/// <summary>Fixed HTTP 202 acknowledgment returned on every exact write retry.</summary>
public sealed record OperationAcceptedResponse(Uuid OperationId, string StatusUrl, DateTimeOffset AcceptedAt);
