namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>The source-backed status of an entry in one published edition.</summary>
public enum CriteriaEntryAuthorityStatus
{
    /// <summary>The entry and its exact parent, if applicable, are accepted at the cut.</summary>
    Accepted,

    /// <summary>Source discovery, ordering, or an individual outcome is unresolved.</summary>
    Pending,

    /// <summary>Complete authoritative evidence establishes that the entry is not a member.</summary>
    NotMember,
}
