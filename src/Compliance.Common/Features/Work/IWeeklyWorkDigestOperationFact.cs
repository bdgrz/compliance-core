using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>One identified receiver admission; NoChange receipts never authorize another delivery effect.</summary>
public interface IWeeklyWorkDigestOperationFact : INativeOperationFact
{
    long Revision { get; }
    ActorAttribution OriginalActor { get; }
    bool NoChange { get; }
    ImmutableContentRef Change { get; }
    ImmutableContentRef ResponseContent { get; }
    ImmutableContentRef? OperationResult { get; }
}
