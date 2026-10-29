using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Rules;

public sealed record IndependenceRulesRevisionContent(Uuid RequestId, long ExpectedSequence, IndependenceRuleVersionView Version);
