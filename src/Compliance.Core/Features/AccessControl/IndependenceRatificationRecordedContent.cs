using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record IndependenceRatificationRecordedContent(Uuid RequestId, long ExpectedSequence, IndependenceRuleRatificationView Ratification);
