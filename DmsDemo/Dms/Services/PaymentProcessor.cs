namespace DmsDemo.Dms.Services;

public sealed class PaymentProcessor
{
    private readonly NotificationService _notifications;

    public PaymentProcessor(NotificationService notifications) =>
        _notifications = notifications;

    public Payment ProcessPayment(
        string requestRef, double amount, string method, string note = "")
    {
        var m = method.Trim().ToLowerInvariant();
        var now = DateTime.Now;
        var ts = now.ToString("yyyy-MM-dd HH:mm:ss");

        if (m is "cash" or "card")
        {
            var receipt = $"REC-{now:yyyyMMddHHmmss}";
            Console.WriteLine(
                $"[Payment] Simulated {m.ToUpperInvariant()} payment of " +
                $"${amount:F2} approved. Receipt: {receipt}");
            return new Payment(requestRef, amount, m, "completed", receipt, ts);
        }
        if (m == "unpaid")
        {
            Console.WriteLine(
                $"[Payment] Request {requestRef} marked UNPAID (${amount:F2}). " +
                "Office notified.");
            _notifications.NotifyOffice("Unpaid Delivery at Pickup",
                $"Request {requestRef} picked up unpaid (${amount:F2}). Note: {note}", ts);
            return new Payment(requestRef, amount, m, "unpaid", "N/A", ts);
        }
        throw new ValidationException(
            $"Invalid payment method '{method}'. Must be cash, card or unpaid.");
    }
}