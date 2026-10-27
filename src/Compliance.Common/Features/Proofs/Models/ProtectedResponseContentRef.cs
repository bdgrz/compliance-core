using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Proofs;

/// <summary>Reference to AES-256-GCM protected response content; the digest covers the encrypted object.</summary>
public sealed record ProtectedResponseContentRef(Uuid ContentId, string CiphertextSha256, long Bytes, string Schema,
    string KeyVersionId);
