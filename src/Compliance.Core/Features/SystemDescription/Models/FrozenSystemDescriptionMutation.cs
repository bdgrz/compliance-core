using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.SystemDescription.Models;

public sealed record FrozenSystemDescriptionMutation(SystemDescriptionView Before, SystemDescriptionView After,
    SystemDescriptionAuthorityHeader Header, ImmutableContentRef CurrentVersion, ImmutableContentRef Payload,
    object Mutation, string Kind, bool Changed)
{
    public SystemDescriptionCurrentAuthority? BeforeAuthority { get; init; }
    public SystemDescriptionCurrentAuthority Authority { get; init; } = null!;
    public ImmutableContentRef AuthorityContent { get; init; } = null!;
    public ImmutableContentRef? SourceSnapshotContent { get; init; }
}
