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
}
