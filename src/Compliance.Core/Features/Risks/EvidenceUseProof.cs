using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>The held fulfilment of one evidence request: the fulfilment fact and the grant that holds it.</summary>
public sealed record EvidenceUseProof(Uuid EvidenceRequestId, NativeFactReference Fulfilment, NativeFactReference Grant);
