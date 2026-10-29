using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.Evidence.Aggregates;

/// <summary>Owns the one terminal protected-ticket outcome for an artifact download command.</summary>
public sealed class ArtifactDownloadGrant : Aggregate
{
    public const string Area = "artifact-download-grants";

    readonly Uuid _tenantId;
    readonly Uuid _artifactId;
    readonly Uuid _commandId;
    EvidenceArtifactDownloadIssuedV2? _issued;
    ArtifactDownloadGrantCanceled? _canceled;

    public ArtifactDownloadGrant(Uuid tenantId, Uuid artifactId, Uuid commandId)
        : base(IdentityOf(tenantId, artifactId, commandId),
            new EventStreamAddress(tenantId.ToString(), Area, IdentityOf(tenantId, artifactId, commandId).ToString()))
    {
        if (tenantId == Uuid.Empty || artifactId == Uuid.Empty || commandId == Uuid.Empty)
            throw new ArgumentException("A download grant requires tenant, artifact, and command identities.");
        _tenantId = tenantId;
        _artifactId = artifactId;
        _commandId = commandId;
        On<EvidenceArtifactDownloadIssuedV2>(ApplyIssued);
        On<ArtifactDownloadGrantCanceled>(ApplyCanceled);
    }

    public static Uuid IdentityOf(Uuid tenantId, Uuid artifactId, Uuid commandId) =>
        Uuid.CreateVersion5(artifactId,
            $"artifact-download-grant:v1:{tenantId}:{commandId}");

    public bool IsIssued => _issued is not null;
    public bool IsCanceled => _canceled is not null;
    public EvidenceArtifactDownloadIssuedV2? Issued => _issued;
    public ArtifactDownloadGrantCanceled? Cancellation => _canceled;

    public Result Issue(Uuid requestId, string contentSha256, long contentLength, string purpose,
        ActorReference issuedBy, DateTimeOffset issuedAt, OperationProvenance operation,
        ImmutableContentRef intent, ImmutableContentRef operationResult)
    {
        var candidate = new EvidenceArtifactDownloadIssuedV2(_tenantId, _artifactId, 1, requestId,
            contentSha256, contentLength, purpose, issuedBy, issuedAt, operation, intent, Id, null,
            operationResult);
        if (_issued is not null)
            return SamePayload(_issued, candidate) ? Result.Success : Conflict("The download grant is already issued.");
        if (_canceled is not null)
            return Conflict("A canceled download grant cannot be issued.");
        var error = ValidateIssued(candidate);
        if (error is not null)
            return Result.Failure(error);
        RaiseEvent(candidate);
        return Result.Success;
    }

    public Result Cancel(long generation, OperationProvenance operation, ImmutableContentRef intent,
        ImmutableContentRef sourceProofs, ImmutableContentRef operationResult)
    {
        var candidate = new ArtifactDownloadGrantCanceled(Stream, 1, generation, intent, sourceProofs,
            operation, Id, null, operationResult);
        if (_canceled is not null)
            return SamePayload(_canceled, candidate) ? Result.Success : Conflict("The download grant is already canceled.");
        if (_issued is not null)
            return Result.Success; // The catalog makes an abort after a terminal outcome a no-op.
        var error = ValidateCanceled(candidate);
        if (error is not null)
            return Result.Failure(error);
        RaiseEvent(candidate);
        return Result.Success;
    }

    void ApplyIssued(EvidenceArtifactDownloadIssuedV2 fact)
    {
        var error = _issued is not null || _canceled is not null ?
            new RequestError(RequestErrorKind.Conflict, "A download grant has one terminal outcome.") :
            ValidateIssued(fact);
        if (error is not null)
            throw new InvalidOperationException("The receiving download grant issue fact is invalid.");
        _issued = fact;
    }

    void ApplyCanceled(ArtifactDownloadGrantCanceled fact)
    {
        var error = _issued is not null || _canceled is not null ?
            new RequestError(RequestErrorKind.Conflict, "A download grant has one terminal outcome.") :
            ValidateCanceled(fact);
        if (error is not null)
            throw new InvalidOperationException("The receiving download grant cancellation fact is invalid.");
        _canceled = fact;
    }

    RequestError? ValidateIssued(EvidenceArtifactDownloadIssuedV2 fact)
    {
        if (fact.TenantId != _tenantId || fact.ArtifactId != _artifactId || fact.GrantId != Id ||
            fact.RequestId != _commandId || fact.Revision != 1 || fact.ContentLength <= 0 ||
            fact.ContentSha256 is not { Length: 64 } || !fact.ContentSha256.All(char.IsAsciiHexDigit) ||
            fact.ContentSha256.Any(char.IsAsciiLetterUpper) ||
            string.IsNullOrWhiteSpace(fact.Purpose) || fact.Purpose.Trim() != fact.Purpose ||
            fact.Purpose.Length > EvidenceRules.MaximumTextLength ||
            fact.IssuedBy is not { Kind: "member", Id.Length: > 0, Display.Length: > 0 and <= 200 } ||
            !Uuid.TryParse(fact.IssuedBy.Id, out var memberId) || memberId == Uuid.Empty ||
            fact.IssuedAt == default || fact.Operation is null || fact.Operation.Owner != Stream ||
            fact.Operation.RootOperationId != _commandId || fact.Operation.LogicalId == Uuid.Empty ||
            fact.Operation.Actor != fact.IssuedBy || fact.Intent is null || fact.OperationResult is null)
            return new RequestError(RequestErrorKind.Validation, "The issued download grant does not match its artifact, command, and ticket.");
        return null;
    }

    RequestError? ValidateCanceled(ArtifactDownloadGrantCanceled fact)
    {
        if (fact.Owner != Stream || fact.GrantId != Id || fact.Revision != 1 || fact.Generation < 1 ||
            fact.Operation is null || fact.Operation.Owner != Stream ||
            fact.Operation.RootOperationId != _commandId || fact.Operation.LogicalId == Uuid.Empty ||
            fact.Intent is null || fact.SourceProofs is null || fact.OperationResult is null)
            return new RequestError(RequestErrorKind.Validation, "The canceled download grant does not match its command and source proofs.");
        return null;
    }

    // Stored facts carry committed metadata that a new candidate lacks, so compare business payload only.
    static bool SamePayload(EvidenceArtifactDownloadIssuedV2 stored, EvidenceArtifactDownloadIssuedV2 candidate) =>
        stored.TenantId == candidate.TenantId && stored.ArtifactId == candidate.ArtifactId &&
        stored.Revision == candidate.Revision && stored.RequestId == candidate.RequestId &&
        stored.ContentSha256 == candidate.ContentSha256 && stored.ContentLength == candidate.ContentLength &&
        stored.Purpose == candidate.Purpose && stored.IssuedBy == candidate.IssuedBy &&
        stored.IssuedAt == candidate.IssuedAt && stored.Operation == candidate.Operation &&
        stored.Intent == candidate.Intent && stored.GrantId == candidate.GrantId &&
        stored.FirstResponse == candidate.FirstResponse && stored.OperationResult == candidate.OperationResult;

    static bool SamePayload(ArtifactDownloadGrantCanceled stored, ArtifactDownloadGrantCanceled candidate) =>
        stored.Owner == candidate.Owner && stored.Revision == candidate.Revision &&
        stored.Generation == candidate.Generation && stored.Intent == candidate.Intent &&
        stored.SourceProofs == candidate.SourceProofs && stored.Operation == candidate.Operation &&
        stored.GrantId == candidate.GrantId && stored.FirstResponse == candidate.FirstResponse &&
        stored.OperationResult == candidate.OperationResult;

    static Result Conflict(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
