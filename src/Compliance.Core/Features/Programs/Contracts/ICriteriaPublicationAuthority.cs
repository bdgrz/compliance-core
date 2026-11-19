using Bdgrz.Compliance.Features.Programs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Contracts;

/// <summary>Reads the authoritative, committed publication of one exact criteria edition.</summary>
public interface ICriteriaPublicationAuthority
{
    /// <summary>Returns evidence of publication or a refusal for the exact edition.</summary>
    ValueTask<Result<CriteriaPublicationProof>> ReadPublishedAsync(Uuid editionId, CancellationToken ct = default);
}
