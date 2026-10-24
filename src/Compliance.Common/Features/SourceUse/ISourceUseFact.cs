using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceUse;

/// <summary>A native fact that grants or settles a source use on its owner stream.</summary>
public interface ISourceUseFact : INativeOperationFact
{
    /// <summary>The source owner stream the use is held on.</summary>
    EventStreamAddress Owner { get; }

    /// <summary>The frozen source authorization generation of the use.</summary>
    long Generation { get; }

    /// <summary>The <see cref="SourceUseAuthorizationIntent" /> the fact points to.</summary>
    ImmutableContentRef Intent { get; }
}
