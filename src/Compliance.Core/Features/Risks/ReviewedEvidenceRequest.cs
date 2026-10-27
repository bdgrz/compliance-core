using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>One cited evidence request in a review's frozen selection: its id and the fulfilment fact the review pinned.</summary>
public sealed record ReviewedEvidenceRequest(Uuid EvidenceRequestId, NativeFactReference Fulfilment);
