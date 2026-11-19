using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>One source proposal awaiting its individually recorded outcome.</summary>
sealed record RuntimeCriteriaPendingIndex(Uuid OperationId, long ExecutionGeneration,
    Uuid ProposalEventId, bool BeforePublication, string Action, Uuid IncarnationId,
    string? Kind, string? Category, string? ParentIdentifier, Uuid? ParentIncarnationId,
    string? Summary);
