using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>The held accepted disposition of one response: the disposition fact and the grant that holds it.</summary>
public sealed record ResponseDispositionProof(Uuid ResponseId, int Version, NativeFactReference Disposition,
    NativeFactReference Grant);
