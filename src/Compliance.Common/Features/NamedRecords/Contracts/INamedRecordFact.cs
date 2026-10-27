using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.NamedRecords.Contracts;

/// <summary>
///     The shared shape of the accepted, canceled, claimed and released facts of a name change. For a record fact
///     <see cref="Owner" /> is the record stream itself; for a claim fact it is the record stream the claim serves,
///     so a claim proves which record holds a name.
/// </summary>
public interface INamedRecordFact : INativeOperationFact
{
    /// <summary>The record stream this fact is about.</summary>
    EventStreamAddress Owner { get; }

    /// <summary>The authored expected revision of the record; zero when the change creates it.</summary>
    long Revision { get; }

    /// <summary>The change generation of the record this fact belongs to.</summary>
    long Generation { get; }

    /// <summary>The frozen <see cref="RecordNameChangeIntent" />.</summary>
    ImmutableContentRef Intent { get; }

    /// <summary>The proof the fact pinned, such as the definite failure behind a cancellation.</summary>
    ImmutableContentRef? SourceProofs { get; }
}
