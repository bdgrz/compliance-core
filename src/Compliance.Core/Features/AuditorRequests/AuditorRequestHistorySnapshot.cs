using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>A verified, transient request history and admission certificate at one exact committed owner cut.</summary>
public sealed class AuditorRequestHistorySnapshot
{
    readonly EventStreamAddress _stream;
    readonly ulong _version;
    readonly ulong _position;
    readonly string? _latestSha256;

    internal AuditorRequestHistorySnapshot(AuditorRequest owner, IReadOnlyList<DomainEvent> facts)
    {
        _stream = owner.Stream;
        _version = owner.Version;
        _position = owner.CommittedStreamPosition;
        _latestSha256 = owner.Latest is { } latest ? ContentDigest.Sha256(latest, latest.GetType()) : null;
        Facts = Array.AsReadOnly(facts.ToArray());
        Responses = Array.AsReadOnly(facts.OfType<AuditorResponseAdmitted>().ToArray());
        Comments = Array.AsReadOnly(facts.OfType<AuditorCommentAdmitted>().ToArray());
        Selections = Array.AsReadOnly(facts.OfType<AuditorSampleSelectionAdmitted>().ToArray());
        ResolutionHistory = Array.AsReadOnly(facts.OfType<AuditorRequestResolved>().ToArray());
    }

    public IReadOnlyList<DomainEvent> Facts { get; }
    public IReadOnlyList<AuditorResponseAdmitted> Responses { get; }
    public IReadOnlyList<AuditorCommentAdmitted> Comments { get; }
    public IReadOnlyList<AuditorSampleSelectionAdmitted> Selections { get; }
    public IReadOnlyList<AuditorRequestResolved> ResolutionHistory { get; }
    public AuditorRequestResolutionAccepted? AcceptedResolution(Uuid id) => Facts.OfType<AuditorRequestResolutionAccepted>()
        .SingleOrDefault(fact => fact.Operation.RootOperationId == id);
    public DomainEvent? TerminalResolution(Uuid id) => Facts.SingleOrDefault(fact =>
        fact is AuditorRequestResolved resolved && resolved.Operation.RootOperationId == id ||
        fact is AuditorRequestResolutionCanceled canceled && canceled.Operation.RootOperationId == id);
    public AuditorSampleSelectionAdmitted? Selection(Uuid id) => Selections.SingleOrDefault(fact => fact.Operation.LogicalId == id);
    public DomainEvent? FactById(Uuid id) => Facts.SingleOrDefault(fact => fact.Metadata.EventId == id);

    internal Result ValidateResponses(AuditorRequest owner, IReadOnlyCollection<Uuid> ids)
    {
        if (!Matches(owner))
            return Stale();
        var admitted = Responses.Select(static fact => fact.Operation.LogicalId).ToHashSet();
        return admitted.Count == 0 || ids.Count != admitted.Count || !admitted.SetEquals(ids)
            ? Conflict("A resolution must name exactly every admitted response, and at least one response must exist.")
            : Result.Success;
    }

    internal Result ValidateSelection(AuditorRequest owner, Uuid selectionId, Uuid? supersedesId, Uuid populationId,
        int populationVersion)
    {
        if (!Matches(owner))
            return Stale();
        if (Selection(selectionId) is not null)
            return Conflict("A sample selection identity cannot be reused.");
        if (supersedesId is null)
            return Result.Success;
        if (Selection(supersedesId.Value) is not { } prior)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The sample selection to amend was not found."));
        return prior.PopulationId != populationId || populationVersion <= prior.PopulationVersion
            ? Conflict("An amended selection must bind a later version of the same audit population.")
            : Result.Success;
    }

    bool Matches(AuditorRequest owner) => owner.Stream == _stream && owner.Version == _version &&
        owner.CommittedStreamPosition == _position && _latestSha256 == (owner.Latest is { } latest
            ? ContentDigest.Sha256(latest, latest.GetType()) : null);
    static Result Stale() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "The verified request history no longer matches its admission cut; retry.", true));
    static Result Conflict(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
