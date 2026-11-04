using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Commitments.Governance.Models;
public sealed record CommitmentSourceCitation(string Kind, string Title, string VersionOrDate,
    string SectionLocator, Uuid? ArtifactId = null);
