using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>The durable status of one client-identified criteria operation.</summary>
/// <remarks>
/// Status is <c>pending</c>, <c>accepted</c>, or <c>refused</c>. An unknown
/// commit or missing participant outcome remains pending until reconciled.
/// </remarks>
public sealed record CriteriaOperationReceipt(Uuid EditionId, Uuid OperationId, string Status, string? Reason = null);
