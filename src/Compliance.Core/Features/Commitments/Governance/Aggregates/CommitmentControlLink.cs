using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Commitments.Governance.Commands;
using Bdgrz.Compliance.Features.Commitments.Governance.Events;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Versioning.Models;
namespace Bdgrz.Compliance.Features.Commitments.Governance.Aggregates;

public sealed class CommitmentControlLink : Aggregate
{
    public const string Area = "commitment-control-links";
    readonly Dictionary<Uuid, (string Intent, Uuid Actor)> _accepted = [];
    public CommitmentControlLink(Uuid tenant, Uuid commitment, Uuid control) : base(IdFor(tenant, commitment, control),
        new EventStreamAddress(tenant.ToString(), Area, IdFor(tenant, commitment, control).ToString()))
    {
        TenantId = tenant; CommitmentId = commitment; ControlId = control;
        On<CommitmentControlLinked>(value => Apply(value, value.TenantId, value.ProgramId, value.CommitmentId, value.ControlId,
            value.Revision, value.CommandId, value.IntentSha256, value.ActorId, value.CommitmentVersion, value.ControlVersion, value.Rationale, true));
        On<CommitmentControlLinkConfirmed>(value => Apply(value, value.TenantId, value.ProgramId, value.CommitmentId, value.ControlId,
            value.Revision, value.CommandId, value.IntentSha256, value.ActorId, value.CommitmentVersion, value.ControlVersion, value.Rationale, true));
        On<CommitmentControlUnlinked>(value => Apply(value, value.TenantId, value.ProgramId, value.CommitmentId, value.ControlId,
            value.Revision, value.CommandId, value.IntentSha256, value.ActorId, value.CommitmentVersion, value.ControlVersion, value.Rationale, false));
    }
    public Uuid TenantId { get; }
    public Uuid ProgramId { get; private set; }
    public Uuid CommitmentId { get; }
    public Uuid ControlId { get; }
    public long Revision { get; private set; }
    public bool IsLinked { get; private set; }
    public long CommitmentVersion { get; private set; }
    public long ControlVersion { get; private set; }
    public string? Rationale { get; private set; }
    public static Uuid IdFor(Uuid tenant, Uuid commitment, Uuid control) =>
        Uuid.CreateVersion5(Uuid.CreateVersion5(tenant, commitment.ToString()), control.ToString());
    public bool HasAccepted(Uuid command) => _accepted.ContainsKey(command);
    public CommandFailure? Link(LinkCommitmentToControl request, long commitmentVersion, long controlVersion,
        ActorReference actor, DateTimeOffset at) => Change(request.CommandId, request.TenantId, request.ProgramId,
        request.CommitmentId, request.ControlId, request.ExpectedRevision, request.Rationale, AuthoredIntentDigest.Compute(request),
        commitmentVersion, controlVersion, "link", actor, at);
    public CommandFailure? Confirm(ConfirmCommitmentControlLink request, long commitmentVersion, long controlVersion,
        ActorReference actor, DateTimeOffset at) => Change(request.CommandId, request.TenantId, request.ProgramId,
        request.CommitmentId, request.ControlId, request.ExpectedRevision, request.Rationale, AuthoredIntentDigest.Compute(request),
        commitmentVersion, controlVersion, "confirm", actor, at);
    public CommandFailure? Unlink(UnlinkCommitmentFromControl request, ActorReference actor, DateTimeOffset at) =>
        Change(request.CommandId, request.TenantId, request.ProgramId, request.CommitmentId, request.ControlId,
            request.ExpectedRevision, request.Reason, AuthoredIntentDigest.Compute(request), CommitmentVersion, ControlVersion, "unlink", actor, at);
    CommandFailure? Change(Uuid command, Uuid tenant, Uuid program, Uuid commitment, Uuid control, long revision,
        string rationale, string digest, long commitmentVersion, long controlVersion, string action, ActorReference actor, DateTimeOffset at)
    {
        if (actor is not { Kind: "member" } || !Uuid.TryParse(actor.Id, out var member) || member == Uuid.Empty)
            return Invalid();
        if (_accepted.TryGetValue(command, out var accepted))
            return accepted == (digest, member) ? null : CommandFailure.StateConflict("Another link command intent already used this identity.");
        if (tenant != TenantId || program == Uuid.Empty || commitment != CommitmentId || control != ControlId ||
            commitment == Uuid.Empty || control == Uuid.Empty || command == Uuid.Empty || revision != Revision ||
            Revision > 0 && program != ProgramId || string.IsNullOrWhiteSpace(rationale) || rationale.Trim().Length > 2000 || at == default ||
            (action == "link" ? IsLinked : !IsLinked) || commitmentVersion <= 0 || controlVersion <= 0 ||
            action == "confirm" && (commitmentVersion < CommitmentVersion || controlVersion < ControlVersion ||
                commitmentVersion == CommitmentVersion && controlVersion == ControlVersion))
            return Invalid();
        DomainEvent fact = action switch
        {
            "link" => new CommitmentControlLinked(tenant, program, commitment, control, Revision + 1, command, digest, member,
                at, commitmentVersion, controlVersion, rationale.Trim()),
            "confirm" => new CommitmentControlLinkConfirmed(tenant, program, commitment, control, Revision + 1, command, digest, member,
                at, commitmentVersion, controlVersion, rationale.Trim()),
            _ => new CommitmentControlUnlinked(tenant, program, commitment, control, Revision + 1, command, digest, member,
                at, commitmentVersion, controlVersion, rationale.Trim()),
        };
        RaiseEvent(fact);
        return null;
    }
    void Apply(DomainEvent fact, Uuid tenant, Uuid program, Uuid commitment, Uuid control, long revision, Uuid command,
        string digest, Uuid actor, long commitmentVersion, long controlVersion, string rationale, bool linked)
    {
        if (tenant != TenantId || program == Uuid.Empty || Revision > 0 && ProgramId != program || commitment != CommitmentId ||
            control != ControlId || revision != Revision + 1 || command == Uuid.Empty || actor == Uuid.Empty || digest.Length != 64 ||
            _accepted.ContainsKey(command) || commitmentVersion <= 0 || controlVersion <= 0 || string.IsNullOrWhiteSpace(rationale) ||
            rationale.Length > 2000 || fact is CommitmentControlLinked && IsLinked || fact is not CommitmentControlLinked && !IsLinked ||
            fact is CommitmentControlLinkConfirmed && (commitmentVersion < CommitmentVersion || controlVersion < ControlVersion ||
                commitmentVersion == CommitmentVersion && controlVersion == ControlVersion))
            throw new InvalidOperationException("The commitment/control link fact has no valid local transition.");
        ProgramId = program; Revision = revision; IsLinked = linked; CommitmentVersion = commitmentVersion;
        ControlVersion = controlVersion; Rationale = rationale; _accepted.Add(command, (digest, actor));
    }
    static CommandFailure Invalid() => CommandFailure.StateConflict("The exact pair, lifecycle or version does not admit this link command.");
}
