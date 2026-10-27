using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.ControlMappings.Models;

/// <summary>The bounded current identities of a mapping or applicability line.</summary>
public sealed record MappingLineHeader(long Revision, int VersionCount, Uuid FirstProposalId,
    MappingLineSlot? Pending, MappingLineSlot? Active, bool EverTerminated);
