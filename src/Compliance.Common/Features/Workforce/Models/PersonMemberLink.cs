using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Models;

/// <summary>One admitted member association, pinned to the Member owner's generation and admission.</summary>
public sealed record PersonMemberLink(Uuid UserId, Uuid MemberId, Uuid LinkId, long Generation, Uuid AdmissionEventId);
