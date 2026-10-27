using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests;

/// <summary>Builds well-formed native registration facts for tests that only need a registered member.</summary>
static class MemberFacts
{
    static readonly ActorReference Actor = ActorReference.ForSystemProcess("system", "system");

    /// <summary>The provenance of a registration of <paramref name="member" /> under a logical id.</summary>
    public static OperationProvenance Provenance(Member member, Uuid commandId, ActorReference? actor = null,
        string authoredIntentSha256 = "")
    {
        var owner = member.Stream;
        return new OperationProvenance(commandId, commandId, null, "RegisterMember", actor ?? Actor, owner,
            "initial", null, authoredIntentSha256.Length == 0 ? new string('a', 64) : authoredIntentSha256,
            new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero), owner, owner);
    }

    /// <summary>Registers the member under a fresh logical id unless one is given.</summary>
    public static Result Register(this Member member, string affiliation = "client_personnel",
        DateOnly? membershipExpiresOn = null, Uuid commandId = default)
    {
        var id = commandId == Uuid.Empty ? Uuid.CreateVersion4() : commandId;
        return member.Register(Provenance(member, id), new ImmutableContentRef(Uuid.CreateVersion4(),
            new string('b', 64), 2, "application/json", "first-response-descriptor.v1"), affiliation,
            membershipExpiresOn);
    }

    public static MemberRegistered Registered(Uuid tenantId, Uuid memberId, Uuid userId,
        string affiliation = "client_personnel", Uuid membershipEpisodeId = default,
        DateOnly? membershipExpiresOn = null)
    {
        var commandId = membershipEpisodeId == Uuid.Empty ? Uuid.CreateVersion4() : membershipEpisodeId;
        var owner = new EventStreamAddress(tenantId.ToString(), "rbac-members", memberId.ToString());
        var operation = new OperationProvenance(commandId, commandId, null, "RegisterMember", Actor, owner,
            "initial", null, new string('a', 64), new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero),
            owner, owner);
        return new MemberRegistered(tenantId, memberId, userId, affiliation, membershipEpisodeId, membershipExpiresOn,
            commandId, operation, null);
    }

    static readonly DateTimeOffset FactAt = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A frozen intent reference of the right schema; nothing reads it back.</summary>
    public static ImmutableContentRef Intent() => new(Uuid.CreateVersion4(), new string('c', 64), 2,
        "application/json", AdministratorTransitionIntent.Schema);

    static OperationProvenance Lifecycle(Uuid tenantId, Uuid memberId, string commandKind)
    {
        var owner = new EventStreamAddress(tenantId.ToString(), "rbac-members", memberId.ToString());
        var id = Uuid.CreateVersion4();
        return new OperationProvenance(id, id, null, commandKind, Actor, owner, "current", null, new string('a', 64),
            FactAt, owner, owner);
    }

    /// <summary>Suspends without any expectation under a fresh logical id.</summary>
    public static Result Suspend(this Member member, Uuid actorMemberId, string actorDisplay, DateTimeOffset at,
        string reason) => member.Suspend(Lifecycle(member.Stream.Realm.ToUuid(), member.Id, "SuspendMember"), Intent(),
        null, actorMemberId, actorDisplay, at, reason, Uuid.Empty, -1);

    /// <summary>Reinstates without any expectation under a fresh logical id.</summary>
    public static Result Reinstate(this Member member, Uuid actorMemberId, string actorDisplay, DateTimeOffset at) =>
        member.Reinstate(Lifecycle(member.Stream.Realm.ToUuid(), member.Id, "ReinstateMember"), Intent(), null,
            actorMemberId, actorDisplay, at, Uuid.Empty, -1);

    /// <summary>Deprovisions without any expectation under a fresh logical id.</summary>
    public static Result Deprovision(this Member member, Uuid actorMemberId, string actorDisplay, DateTimeOffset at,
        string reason) => member.Deprovision(Lifecycle(member.Stream.Realm.ToUuid(), member.Id, "DeprovisionMember"),
        Intent(), null, actorMemberId, actorDisplay, at, reason, Uuid.Empty, -1);

    static Uuid ToUuid(this string value) => Uuid.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    public static MemberSuspended Suspended(Uuid tenantId, Uuid memberId, Uuid userId, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset at, string reason) => new(tenantId, memberId, userId, actorMemberId,
        actorDisplay, at, reason, Lifecycle(tenantId, memberId, "SuspendMember"), Intent(), null, null);

    public static MemberReinstated Reinstated(Uuid tenantId, Uuid memberId, Uuid userId, string affiliation,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset at) => new(tenantId, memberId, userId, affiliation,
        actorMemberId, actorDisplay, at, Lifecycle(tenantId, memberId, "ReinstateMember"), Intent(), null, null);

    public static MemberDeprovisioned Deprovisioned(Uuid tenantId, Uuid memberId, Uuid userId, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset at, string reason, Uuid membershipEpisodeId) => new(tenantId, memberId,
        userId, actorMemberId, actorDisplay, at, reason, membershipEpisodeId,
        Lifecycle(tenantId, memberId, "DeprovisionMember"), Intent(), null, null);
}
