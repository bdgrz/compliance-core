using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record IndependenceRulesRevisionContent(Uuid RequestId, long ExpectedSequence, IndependenceRuleVersionView Version);
