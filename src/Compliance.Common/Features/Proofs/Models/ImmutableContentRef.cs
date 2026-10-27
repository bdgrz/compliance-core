using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Proofs.Models;

/// <summary>Digest-addressed reference to immutable content with a declared media type and schema.</summary>
public sealed record ImmutableContentRef(Uuid ContentId, string Sha256, long Bytes, string MediaType, string Schema);
