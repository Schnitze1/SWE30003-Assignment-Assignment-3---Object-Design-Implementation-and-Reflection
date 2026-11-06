namespace DmsDemo.Dms.Services;

public sealed class JobAllocator
{
    private readonly UserDirectory _users;
    private readonly RequestRegistry _registry;
    private readonly NotificationService _notifications;

    public JobAllocator(UserDirectory users, RequestRegistry reg, NotificationService notif)
    {
        _users = users; _registry = reg; _notifications = notif;
    }

    public int GetActiveJobCount(string driverId) =>
        _registry.ListRequests()
            .Where(r => r.State.Name is "Assigned" or "PickedUp")
            .Count(r => r.CurrentAssignment()?.DriverId == driverId);

    public List<DeliveryStaff> ListEligibleDrivers(DeliveryRequest request)
    {
        var pickup = DateTime.ParseExact(
            $"{request.PickupDate} {request.PickupTime}", "yyyy-MM-dd HH:mm",
            System.Globalization.CultureInfo.InvariantCulture);
        return _users.ListDrivers()
            .Where(d => d.Active)
            .Where(d => _users.GetDriverAvailabilities(d.Id)
                .Any(a => a.Period.Contains(pickup)))
            .Where(d => GetActiveJobCount(d.Id) < 5)
            .OrderBy(d => GetActiveJobCount(d.Id))
            .ThenBy(d => d.Id)
            .ToList();
    }

    public DeliveryStaff Allocate(DeliveryRequest request, string? driverId = null, string actor = "office")
    {
        var eligible = ListEligibleDrivers(request);
        DeliveryStaff driver;
        if (driverId is not null)
        {
            driver = eligible.FirstOrDefault(d => d.Id == driverId)
                ?? throw new NoDriverAvailableException(
                    $"Driver '{driverId}' is not eligible or available.");
        }
        else
        {
            if (eligible.Count == 0)
            {
                var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                _notifications.NotifyOffice("No Driver Available",
                    $"No driver available for request {request.Ref}.", ts);
                throw new NoDriverAvailableException(
                    $"No driver available for request {request.Ref}.");
            }
            driver = eligible[0];
        }

        var newState = request.State.Allocate(actor);
        var assignment = new DeliveryAssignment(
            driver.Id, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), true);
        request.Assignments.Add(assignment);
        request.TransitionTo(newState, actor, $"Allocated to driver {driver.Id}");
        return driver;
    }

    public DeliveryStaff Reassign(DeliveryRequest request, string driverId, string reason, string actor = "office")
    {
        var eligible = ListEligibleDrivers(request);
        var driver = eligible.FirstOrDefault(d => d.Id == driverId)
            ?? throw new NoDriverAvailableException(
                $"Driver '{driverId}' is not eligible or available.");

        var newState = request.State.Reassign(actor);
        var current = request.CurrentAssignment();
        if (current is not null)
        {
            current.Active = false;
            current.ReassignmentReason = reason;
        }
        request.Assignments.Add(new DeliveryAssignment(
            driver.Id, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), true));
        request.TransitionTo(newState, actor, $"Reassigned to {driver.Id}: {reason}");
        return driver;
    }

    public DeliveryStaff ReallocateAfterFailure(
        DeliveryRequest request, string? driverId = null, string actor = "office")
    {
        var eligible = ListEligibleDrivers(request);
        DeliveryStaff driver;
        if (driverId is not null)
        {
            driver = eligible.FirstOrDefault(d => d.Id == driverId)
                ?? throw new NoDriverAvailableException(
                    $"Driver '{driverId}' is not eligible or available.");
        }
        else
        {
            if (eligible.Count == 0)
            {
                var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                _notifications.NotifyOffice("No Driver Available",
                    $"No driver available for request {request.Ref}.", ts);
                throw new NoDriverAvailableException(
                    $"No driver available for request {request.Ref}.");
            }
            driver = eligible[0];
        }

        var newState = request.State.Reallocate(actor);
        var current = request.CurrentAssignment();
        if (current is not null) current.Active = false;
        request.Assignments.Add(new DeliveryAssignment(
            driver.Id, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), true));
        request.TransitionTo(newState, actor, $"Reallocated to driver {driver.Id}");
        return driver;
    }
}