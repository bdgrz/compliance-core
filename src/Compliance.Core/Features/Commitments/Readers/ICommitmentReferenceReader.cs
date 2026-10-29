using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
namespace Bdgrz.Compliance.Features.Commitments.Readers;

/// <summary>Lets other contexts verify a commitment reference against the authoritative stream.</summary>
public interface ICommitmentReferenceReader
{
    ValueTask<bool> IsRecordedAsync(Uuid tenantId, Uuid draftId, CancellationToken ct = default);
}
