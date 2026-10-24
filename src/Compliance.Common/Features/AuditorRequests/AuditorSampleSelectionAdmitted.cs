using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     The open request accepted one exact sample selection. <paramref name="Intent" /> is the frozen
///     <c>AuditorSampleSelectionIntent</c> with the complete population version content and the nonempty duplicate-free
///     row selection. The population, its version and the superseded selection are repeated as facts so the request can
///     enforce amendment lineage from its own ordered stream. The logical id of <paramref name="Operation" /> is the
///     selection id.
/// </summary>
[Discriminator("bdgrz.auditor-request.auditor-sample-selection-admitted", 1)]
public sealed record AuditorSampleSelectionAdmitted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, Uuid PopulationId,
    int PopulationVersion, Uuid? SupersedesSelectionId) : DomainEvent, INativeOperationFact;
