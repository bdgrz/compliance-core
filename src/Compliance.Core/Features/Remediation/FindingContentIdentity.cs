using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>The immutable identity of one retained finding revision.</summary>
public sealed record FindingContentIdentity(Uuid VersionId, long Revision, string ContentSha256);

/// <summary>Creates a stable source identity without time-derived readiness labels.</summary>
public static class FindingContentIdentityFactory
{
    const string Domain = "bdgrz.remediation.finding-source.v1\0";

    public static FindingContentIdentity Create(FindingView finding,
        IReadOnlyList<FindingClosureView> closures)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ArgumentNullException.ThrowIfNull(closures);
        var content = new FindingSourceContent(finding.TenantId, finding.ProgramId,
            finding.FindingId, finding.Revision, finding.Source, finding.Title,
            finding.Description, finding.Severity, finding.AffectedScope,
            finding.OwnerMemberId, finding.DueOn, finding.RootCause, finding.Status,
            finding.Links, finding.CorrectiveActions,
            finding.Acceptances.Select(item => item with { State = "recorded" }).ToArray(),
            closures, finding.History, finding.RaisedBy, finding.RaisedAt);
        var payload = JsonSerializer.SerializeToUtf8Bytes(content);
        var domain = Encoding.UTF8.GetBytes(Domain);
        var material = new byte[domain.Length + payload.Length];
        domain.CopyTo(material, 0);
        payload.CopyTo(material, domain.Length);
        var digest = Convert.ToHexStringLower(SHA256.HashData(material));
        var versionId = Uuid.CreateVersion5(finding.FindingId,
            $"finding-source-v1:{finding.Revision}:{digest}");
        return new(versionId, finding.Revision, digest);
    }

    sealed record FindingSourceContent(Uuid TenantId, Uuid ProgramId, Uuid FindingId,
        long Revision, FindingSource Source, string Title, string Description,
        string Severity, string AffectedScope, Uuid OwnerMemberId, DateOnly DueOn,
        string? RootCause, string Status, IReadOnlyList<FindingLink> Links,
        IReadOnlyList<CorrectiveActionView> CorrectiveActions,
        IReadOnlyList<FindingAcceptanceView> Acceptances,
        IReadOnlyList<FindingClosureView> Closures,
        IReadOnlyList<FindingHistoryEntryView> History, AccessControl.Models.ActorReference RaisedBy,
        DateTimeOffset RaisedAt);
}
