using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;

/// <summary>Deterministic source-boundary stand-in for control-mapping handler tests.</summary>
sealed class TestCriteriaEntryAuthority(ICriteriaCatalog catalog,
    CriteriaEntryAuthorityStatus? forcedStatus = null) : ICriteriaEntryAuthority
{
    public ValueTask<Result<CriteriaEntryAuthorityResult>> ReadAsync(Uuid editionId, string identifier,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (forcedStatus is { } forced && forced != CriteriaEntryAuthorityStatus.Accepted)
            return Completed(new CriteriaEntryAuthorityResult(forced, null));
        var entry = catalog.GetEntry(editionId, identifier);
        if (entry is null)
            return Completed(new CriteriaEntryAuthorityResult(CriteriaEntryAuthorityStatus.NotMember, null));
        var isPoint = entry.Kind == "point_of_focus";
        var proof = new CriteriaEntryProof(entry, Id("incarnation", editionId, identifier),
            Id("publication", editionId, ""), Id("proposal", editionId, identifier),
            Id("decision", editionId, identifier),
            isPoint ? Id("incarnation", editionId, entry.ParentIdentifier!) : null,
            isPoint ? Id("proposal", editionId, entry.ParentIdentifier!) : null,
            isPoint ? Id("decision", editionId, entry.ParentIdentifier!) : null);
        return Completed(new CriteriaEntryAuthorityResult(CriteriaEntryAuthorityStatus.Accepted, proof));
    }

    static Uuid Id(string kind, Uuid editionId, string identifier) =>
        Uuid.CreateVersion5(editionId, $"test-criteria-{kind}:{identifier}");

    static ValueTask<Result<CriteriaEntryAuthorityResult>> Completed(CriteriaEntryAuthorityResult value) =>
        ValueTask.FromResult(Result<CriteriaEntryAuthorityResult>.Success(value));
}
