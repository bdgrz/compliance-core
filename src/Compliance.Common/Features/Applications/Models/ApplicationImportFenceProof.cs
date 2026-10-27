using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>The native fence fact of one target, as listed by a cancellation's fence manifest.</summary>
public sealed record ApplicationImportFenceProof(Uuid ApplicationId, Uuid EventId, ulong EventVersion);
