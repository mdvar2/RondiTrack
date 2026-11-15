namespace RondiTrack.DTOs.Payouts;

// GET /stokvels/{stokvelId}/cycles/{cycleId}/payouts: the stokvel's payout
// rotation as seen from one cycle. Items come in rotation order, and the
// server decides every status, including who is next, so no client has to
// work it out (and get it wrong).
public record PayoutOrderResponse(
    Guid StokvelId,
    Guid CycleId,
    IReadOnlyList<PayoutOrderEntryResponse> Items);

// Position is 1-based. AmountCents is the payout for a Paid member, and the
// cycle's target pot for everyone still to be paid. PaidAtUtc is set only
// for Paid.
public record PayoutOrderEntryResponse(
    int Position,
    Guid UserId,
    string MemberName,
    long AmountCents,
    DateTime? PaidAtUtc,
    string Status);

public static class PayoutStatuses
{
    // Already paid out, in any cycle of this stokvel.
    public const string Paid = "Paid";

    // The member the next POST .../payouts pays. At most one.
    public const string Next = "Next";

    // In the rotation, after Next.
    public const string Waiting = "Waiting";
}
