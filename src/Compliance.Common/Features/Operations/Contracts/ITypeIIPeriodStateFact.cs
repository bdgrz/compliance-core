using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Operations.Contracts;

/// <summary>One compact state advance at an exact period native sequence.</summary>
public interface ITypeIIPeriodStateFact : INativeOperationFact
{
    Uuid TenantId { get; }
    Uuid SubjectId { get; }
    long Revision { get; }
    Uuid? ActorMemberId { get; }
    DateTimeOffset OccurredAt { get; }
    long NativeSequence { get; }
    TypeIIPeriodAuthorityHeader Header { get; }
    ImmutableContentRef CurrentContent { get; }
    ImmutableContentRef? OperationResult { get; }
}
