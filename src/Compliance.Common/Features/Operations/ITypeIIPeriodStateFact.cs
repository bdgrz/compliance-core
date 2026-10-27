using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

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
