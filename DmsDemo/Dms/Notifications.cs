using System.Text.Json;

namespace DmsDemo.Dms;

public interface IStatusObserver
{
    void OnStatusChange(DeliveryRequest request, StatusRecord record);
}

public sealed class NotificationMessage
{
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Timestamp { get; set; } = "";
}

public sealed class NotificationService : IStatusObserver
{
    public Dictionary<string, List<NotificationMessage>> Inboxes { get; }

    public NotificationService(
        Dictionary<string, List<NotificationMessage>>? inboxes = null)
    {
        Inboxes = inboxes ?? new();
    }

    public void Notify(string recipient, string title, string message, string? timestamp = null)
    {
        var ts = timestamp ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        if (!Inboxes.ContainsKey(recipient)) Inboxes[recipient] = new();
        Inboxes[recipient].Add(new NotificationMessage
        {
            Title = title,
            Message = message,
            Timestamp = ts
        });
    }

    public void NotifyOffice(string title, string message, string? timestamp = null) =>
        Notify("office", title, message, timestamp);

    public List<NotificationMessage> GetInbox(string userId)
    {
        var results = new List<NotificationMessage>();
        if (userId.StartsWith("O-") || userId == "office")
        {
            results.AddRange(Inboxes.GetValueOrDefault("office", new()));
            if (userId != "office")
                results.AddRange(Inboxes.GetValueOrDefault(userId, new()));
        }
        else
            results.AddRange(Inboxes.GetValueOrDefault(userId, new()));
        return results.OrderBy(m => m.Timestamp).ToList();
    }

    public void OnStatusChange(DeliveryRequest request, StatusRecord record)
    {
        var ts = record.Timestamp;
        var @ref = request.Ref;

        switch (record.StateName)
        {
            case "Requested":
                if (request.History.Count <= 1)
                    NotifyOffice("New Delivery Request Logged",
                        $"Request {@ref} logged by customer {request.CustomerPhone}.", ts);
                else
                    NotifyOffice("Request Returned to Requested",
                        $"Request {@ref} returned to Requested: {record.Note}", ts);
                break;
            case "Quoted":
                Notify(request.CustomerPhone, "Quote Issued",
                    $"A quote has been issued for request {@ref}.", ts);
                break;
            case "Accepted":
                NotifyOffice("Quote Accepted",
                    $"Customer accepted quote for request {@ref}.", ts);
                break;
            case "Rejected":
                NotifyOffice("Quote Rejected",
                    $"Customer rejected quote for request {@ref}.", ts);
                break;
            case "Assigned":
                var driverId = request.CurrentAssignment()?.DriverId ?? "Driver";
                Notify(request.CustomerPhone, "Driver Assigned",
                    $"Driver {driverId} has been assigned to delivery {@ref}.", ts);
                Notify(driverId, "New Job Assigned",
                    $"You have been assigned to delivery {@ref}.", ts);
                break;
            case "PickedUp":
                Notify(request.CustomerPhone, "Parcels Picked Up",
                    $"Your delivery {@ref} has been picked up and is in transit.", ts);
                break;
            case "PickupFailed":
                var pf = $"Pickup failed for request {@ref}: {record.Note}";
                NotifyOffice("Pickup Failed", pf, ts);
                Notify(request.CustomerPhone, "Pickup Failed", pf, ts);
                break;
            case "Delivered":
                Notify(request.CustomerPhone, "Delivery Completed",
                    $"Delivery {@ref} has been successfully delivered: {record.Note}", ts);
                break;
            case "DeliveryFailed":
                var df = $"Delivery failed for request {@ref}: {record.Note}";
                NotifyOffice("Delivery Failed", df, ts);
                Notify(request.CustomerPhone, "Delivery Failed", df, ts);
                break;
            case "Cancelled":
                var c = $"Request {@ref} cancelled by {record.Actor}: {record.Note}";
                NotifyOffice("Request Cancelled", c, ts);
                var assign = request.CurrentAssignment();
                if (assign is { Active: true }) Notify(assign.DriverId, "Job Cancelled", c, ts);
                break;
        }
    }
    public void Restore(Dictionary<string, List<NotificationMessage>> inboxes)
    {
        Inboxes.Clear();
        foreach (var (k, v) in inboxes) Inboxes[k] = v;
    }
}