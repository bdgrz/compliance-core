namespace Bdgrz.Compliance.Features.Risks;

/// <summary>Verified read composition; publication history is never command-owner state.</summary>
public sealed record RiskMethodReadSnapshot(IReadOnlyList<RiskMethodVersionView> Versions,
    IReadOnlyList<RiskMethodVersionPublished> Facts)
{
    public long PublishedVersion => Versions.Count;
    public RiskMethodVersionView? Current => Versions.Count == 0 ? null : Versions[^1];
    public RiskMethodVersionView? GetVersion(long version) => version > 0 && version <= Versions.Count ? Versions[(int)version - 1] : null;
    public RiskMethodVersionView? Find(Cntryl.Portia.Uuid id) => Versions.FirstOrDefault(version => version.MethodVersionId == id);
}
