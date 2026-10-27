using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.ControlMappings.Models;

/// <summary>The exact current proposal identity and proposing member.</summary>
public sealed record MappingLineSlot(Uuid ProposalId, int VersionNumber, Uuid ProposerMemberId);
