using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Applications.Models;

public sealed record ApplicationImportEffectProof(Uuid RowId, Uuid ApplicationId,
    Uuid EventId, ulong EventVersion);
