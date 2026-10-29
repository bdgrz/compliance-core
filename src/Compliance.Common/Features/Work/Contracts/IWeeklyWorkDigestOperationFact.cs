using Cntryl.Portia;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Work.Contracts;

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
