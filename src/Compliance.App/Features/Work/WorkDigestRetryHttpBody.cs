using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>The authored retry evidence; owner and command identities come from the canonical route.</summary>
sealed record WorkDigestRetryHttpBody(
    [property: JsonRequired] string EvidenceReference,
    [property: JsonRequired] string Rationale,
    [property: JsonRequired] long ExpectedRevision);
