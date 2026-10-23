namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The source revisions an admission pins into its durable intent after the source preflight passed against
///     the hydrated owners and current guard state.
/// </summary>
public sealed record AdministratorSourceBinding(ulong MemberRevision, ulong? AssignmentGeneration,
    ulong? ExpectedAssignmentRevision, string? Reason);
