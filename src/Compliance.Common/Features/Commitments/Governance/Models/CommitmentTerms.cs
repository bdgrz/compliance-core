using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Commitments.Governance.Models;
public sealed record CommitmentTerms(string Statement, Uuid ServiceId, Uuid OwnerPersonId,
    Uuid BoundaryId, IReadOnlyList<string> Categories, CommitmentSourceCitation Citation, Uuid? ProviderId = null);
