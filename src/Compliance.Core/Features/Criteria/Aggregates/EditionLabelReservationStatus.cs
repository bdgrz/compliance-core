namespace Bdgrz.Compliance.Features.Criteria.Aggregates;

/// <summary>The durable outcome of one edition label reservation operation.</summary>
public enum EditionLabelReservationStatus
{
    /// <summary>The reservation is held pending edition confirmation.</summary>
    Reserved,

    /// <summary>The reservation is held and its edition change was confirmed.</summary>
    Confirmed,

    /// <summary>The reservation was released and an exact retry cannot reacquire it.</summary>
    Released,
}
