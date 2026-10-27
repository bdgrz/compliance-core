using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
namespace Bdgrz.Compliance.Features.SourceUse.Contracts;

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
