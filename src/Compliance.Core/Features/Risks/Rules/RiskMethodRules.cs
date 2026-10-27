using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Rules;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Models;
namespace Bdgrz.Compliance.Features.Risks.Rules;

/// <summary>The program's versioned M0-D10 method: qualitative 5x5 with optional appetite.</summary>
public sealed class RiskMethodRules
{
    public const string QualitativeFiveByFive = "qualitative_5x5";
    public const string AnnualReassessment = "P1Y";
    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly List<RiskMethodVersionView> _versions = [];

    public Uuid Id => RiskMethod.IdFor(_tenantId, _programId);
    public Uuid TenantId => _tenantId;
    public Uuid ProgramId => _programId;
    int _originalVersion;
    public IReadOnlyList<RiskMethodVersionView> PendingVersions => _versions.Skip(_originalVersion).ToArray();
    public static RiskMethodRules FromVersions(Uuid tenant, Uuid program, IReadOnlyList<RiskMethodVersionView> versions)
    {
        var rules = new RiskMethodRules(tenant, program);
        rules._versions.AddRange(versions);
        rules._originalVersion = versions.Count;
        return rules;
    }
    public IReadOnlyList<RiskMethodVersionView> Versions => _versions.AsReadOnly();
    public long PublishedVersion => _versions.Count;
    public RiskMethodVersionView? Current => _versions.Count == 0 ? null : _versions[^1];

    public RiskMethodRules(Uuid tenantId, Uuid programId)
    {
        _tenantId = tenantId;
        _programId = programId;
    }

    public static Uuid IdFor(Uuid tenantId, Uuid programId) =>
        Uuid.CreateVersion5(tenantId, $"risk-method:{programId}");

    public RiskMethodVersionView? GetVersion(long version) =>
        version >= 1 && version <= _versions.Count ? _versions[(int)version - 1] : null;

    public RiskMethodVersionView? Find(Uuid methodVersionId) =>
        _versions.Find(version => version.MethodVersionId == methodVersionId);

    public CommandFailure? Publish(Uuid programId, long expectedVersion,
        IReadOnlyList<string>? likelihoodScale, IReadOnlyList<string>? impactScale,
        int? appetiteThreshold, ActorReference publishedBy, DateTimeOffset publishedAt)
    {
        if (programId != _programId)
            return CommandFailure.MissingRecord("The risk method was not found.");
        if (expectedVersion != PublishedVersion)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("risk method",
                PublishedVersion));
        if (!ValidScale(likelihoodScale) || !ValidScale(impactScale))
            return CommandFailure.InvalidContent(
                "A qualitative 5x5 method requires five likelihood and five impact descriptors of at most 200 characters.");
        if (appetiteThreshold is < 1 or > 25)
            return CommandFailure.InvalidContent(
                "The appetite threshold must be a residual score from 1 to 25.");
        if (publishedBy is null || publishedAt == default)
            return CommandFailure.InvalidContent("A method requires attributable bounded publication identity.");
        var version = PublishedVersion + 1;
        _versions.Add(new RiskMethodVersionView(_tenantId, programId,
                Uuid.CreateVersion5(Id, version.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                version, QualitativeFiveByFive, [.. likelihoodScale!.Select(static d => d.Trim())],
                [.. impactScale!.Select(static d => d.Trim())], appetiteThreshold,
                AnnualReassessment, publishedBy, publishedAt));
        return null;
    }

    static bool ValidScale(IReadOnlyList<string>? scale) =>
        scale is { Count: 5 } && scale.All(static descriptor =>
            !string.IsNullOrWhiteSpace(descriptor) && descriptor.Trim().Length <= 200);
}
