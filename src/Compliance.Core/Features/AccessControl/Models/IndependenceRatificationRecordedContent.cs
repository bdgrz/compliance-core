using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

public sealed record IndependenceRatificationRecordedContent(Uuid RequestId, long ExpectedSequence, IndependenceRuleRatificationView Ratification);
