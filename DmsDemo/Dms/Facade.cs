using DmsDemo.Dms.Services;

namespace DmsDemo.Dms;

public sealed class DeliveryManagementSystem
{
    public JsonStore Store { get; }
    public ServiceArea ServiceArea { get; } = new();
    public NotificationService Notifications { get; }
    public UserDirectory Users { get; private set; }
    public RequestRegistry Registry { get; private set; }
    public StandardPricing StandardPricing { get; } = new();
    public ExpressPricing ExpressPricing { get; }
    public QuoteCalculator QuoteCalculator { get; } = new();
    public RequestFactory Factory { get; private set; }
    public JobAllocator Allocator { get; private set; }
    public PaymentProcessor Payments { get; private set; }

    public DeliveryManagementSystem(JsonStore? store = null, bool reset = false)
    {
        Store = store ?? new JsonStore();
        Notifications = new NotificationService();
        Users = new UserDirectory();
        Registry = new RequestRegistry();
        ExpressPricing = new ExpressPricing(StandardPricing);
        Factory = new RequestFactory(ServiceArea, Users, Registry);
        Allocator = new JobAllocator(Users, Registry, Notifications);
        Payments = new PaymentProcessor(Notifications);
        Registry.AttachObserver(Notifications);

        if (reset || !Store.Exists()) Seed();
        else Load();
    }

    private void Seed()
    {
        Users.SeedDefaults();
        Save();
    }

    private void Save()
    {
        var data = new PersistedData
        {
            Users = new PersistedUsers
            {
                Customers = Users.Customers,
                Staff = Users.Staff,
                Availabilities = Users.Availabilities,
            },
            Registry = new PersistedRegistry
            {
                Counter = Registry.Counter,
                Requests = Registry.Requests,
            },
            Notifications = new PersistedNotifications { Inboxes = Notifications.Inboxes },
        };
        Store.Save(DmsJson.Serialize(data));
    }

    private void Load()
    {
        var raw = Store.Load();
        if (raw is null) { Seed(); return; }
        var data = DmsJson.Deserialize<PersistedData>(raw);
        if (data is null) { Seed(); return; }

        Users = new UserDirectory();
        Users.Restore(data.Users.Customers, data.Users.Staff, data.Users.Availabilities);

        Registry = new RequestRegistry();
        Registry.Restore(data.Registry.Requests, data.Registry.Counter);

        Notifications.Restore(data.Notifications.Inboxes);
        Registry.AttachObserver(Notifications);

        Factory = new RequestFactory(ServiceArea, Users, Registry);
        Allocator = new JobAllocator(Users, Registry, Notifications);
        Payments = new PaymentProcessor(Notifications);
    }

    // ---- Customer ops ----
    public Customer? IdentifyCustomer(string phone) => Users.FindCustomer(phone);

    public Customer RegisterCustomer(string name, string phone, string email = "")
    {
        var c = Users.RegisterCustomer(name, phone, email);
        Save();
        return c;
    }

    public Customer UpdateCustomerContact(string phone, string name, string email = "")
    {
        var c = Users.UpdateCustomerContact(phone, name, email);
        Save();
        return c;
    }

    // ---- Request ops ----
    public DeliveryRequest SubmitRequest(string phone, RequestInput input)
    {
        var r = Factory.CreateRequest(phone, input);
        Save();
        return r;
    }

    public List<DeliveryRequest> ListRequests(string? phone = null, string? state = null) =>
        Registry.ListRequests(phone, state);

    public DeliveryRequest GetRequest(string @ref) => Registry.GetRequest(@ref);

    public Quote GenerateQuote(string @ref, string strategy,
        double? overrideAmount = null, string? overrideReason = null)
    {
        var request = GetRequest(@ref);
        if (request.State.Name == "Quoted")
            request.TransitionTo(request.State.Requote("office"), "office", "Re-quoting request");

        var newState = request.State.IssueQuote("office");
        var strat = strategy.Trim().ToLowerInvariant() == "express"
            ? (IPricingStrategy)ExpressPricing : StandardPricing;
        var quote = QuoteCalculator.CalculateQuote(
            request.Parcels, strat, overrideAmount, overrideReason);
        request.AddQuote(quote);
        var note = $"Quote issued: ${quote.Amount:F2} ({quote.Strategy})";
        if (!string.IsNullOrEmpty(quote.OverrideReason))
            note += $" [Override: {quote.OverrideReason}]";
        request.TransitionTo(newState, "office", note);
        Save();
        return quote;
    }

    public DeliveryRequest RespondToQuote(string @ref, bool accept)
    {
        var request = GetRequest(@ref);
        var current = request.CurrentQuote();
        if (current is null || current.Status != "open")
            throw new ValidationException("No open quote available to respond to.");

        RequestState newState;
        string note;
        if (accept)
        {
            newState = request.State.AcceptQuote(request.CustomerPhone);
            current.Status = "accepted";
            note = $"Customer accepted quote (${current.Amount:F2})";
        }
        else
        {
            newState = request.State.RejectQuote(request.CustomerPhone);
            current.Status = "rejected";
            note = "Customer rejected quote";
        }
        request.TransitionTo(newState, request.CustomerPhone, note);
        Save();
        return request;
    }

    public List<DeliveryStaff> ListEligibleDrivers(string @ref) =>
        Allocator.ListEligibleDrivers(GetRequest(@ref));

    public DeliveryStaff AllocateDriver(string @ref, string? driverId = null)
    {
        var d = Allocator.Allocate(GetRequest(@ref), driverId, "office");
        Save();
        return d;
    }

    public DeliveryStaff ReassignDriver(string @ref, string driverId, string reason)
    {
        var d = Allocator.Reassign(GetRequest(@ref), driverId, reason, "office");
        Save();
        return d;
    }

    public DeliveryStaff ReallocateAfterFailure(string @ref, string? driverId = null)
    {
        var d = Allocator.ReallocateAfterFailure(GetRequest(@ref), driverId, "office");
        Save();
        return d;
    }

    public Payment? RecordPickup(string @ref, string driverId, bool parcelsMatch,
        string paymentMethod = "cash", string note = "")
    {
        var request = GetRequest(@ref);
        var assign = request.CurrentAssignment();
        if (assign is null || assign.DriverId != driverId)
            throw new ValidationException(
                $"Request {@ref} is not assigned to driver {driverId}.");

        if (!parcelsMatch)
        {
            var newState = request.State.ParcelMismatch(driverId);
            foreach (var p in request.Parcels)
            {
                p.Mismatch = true;
                p.MismatchNote = note;
            }
            assign.Active = false;
            request.TransitionTo(newState, driverId,
                $"Parcel mismatch flagged: {note}");
            Save();
            return null;
        }

        var quote = request.CurrentQuote();
        var amt = quote?.Amount ?? 0.0;
        var payment = Payments.ProcessPayment(@ref, amt, paymentMethod, note);
        request.Payments.Add(payment);
        var picked = request.State.PickupDone(driverId);
        var msg = $"Pickup completed. Payment: {payment.Method} ({payment.Status})";
        if (!string.IsNullOrEmpty(note)) msg += $" - {note}";
        request.TransitionTo(picked, driverId, msg);
        Save();
        return payment;
    }

    public void RecordPickupFailed(string @ref, string driverId, string note)
    {
        var request = GetRequest(@ref);
        var assign = request.CurrentAssignment();
        if (assign is null || assign.DriverId != driverId)
            throw new ValidationException(
                $"Request {@ref} is not assigned to driver {driverId}.");
        var newState = request.State.PickupFailed(driverId);
        request.TransitionTo(newState, driverId, $"Pickup failed: {note}");
        Save();
    }

    public ProofOfDelivery RecordDelivery(string @ref, string driverId, string receivedBy)
    {
        var request = GetRequest(@ref);
        var assign = request.CurrentAssignment();
        if (assign is null || assign.DriverId != driverId)
            throw new ValidationException(
                $"Request {@ref} is not assigned to driver {driverId}.");
        var name = Validation.Name(receivedBy, "Received by");
        var proof = ProofOfDelivery.Create(name);
        request.Proof = proof;
        var newState = request.State.DeliverSuccess(driverId);
        assign.Active = false;
        request.TransitionTo(newState, driverId, $"Delivered to {name}");
        Save();
        return proof;
    }

    public void RecordDeliveryFailed(string @ref, string driverId, string reason)
    {
        var request = GetRequest(@ref);
        var assign = request.CurrentAssignment();
        if (assign is null || assign.DriverId != driverId)
            throw new ValidationException(
                $"Request {@ref} is not assigned to driver {driverId}.");
        var trimmed = reason.Trim();
        if (trimmed.Length == 0)
            throw new ValidationException("Failure reason cannot be blank.");
        var newState = request.State.DeliverFailed(driverId);
        request.TransitionTo(newState, driverId, $"Delivery failed: {trimmed}");
        Save();
    }

    public DeliveryRequest RetryDelivery(string @ref)
    {
        var request = GetRequest(@ref);
        var newState = request.State.RetryDelivery("office");
        request.TransitionTo(newState, "office", "Delivery retry authorized by office");
        Save();
        return request;
    }

    public DeliveryRequest CancelRequest(string @ref, string actor, string reason)
    {
        var request = GetRequest(@ref);
        var isOffice = actor.StartsWith("O-") || actor == "office";
        var newState = request.State.Cancel(actor, isOffice);
        var assign = request.CurrentAssignment();
        if (assign is not null) assign.Active = false;
        request.TransitionTo(newState, actor, $"Cancelled: {reason.Trim()}");
        Save();
        return request;
    }

    public List<StatusRecord> Track(string @ref) => Registry.Track(@ref);

    public Availability SetAvailability(string driverId, DateTime start, DateTime end)
    {
        var a = Users.AddAvailability(driverId, start, end);
        Save();
        return a;
    }

    public Availability RemoveAvailability(string driverId, int index)
    {
        var a = Users.RemoveAvailability(driverId, index);
        Save();
        return a;
    }

    public List<NotificationMessage> Inbox(string userId) => Notifications.GetInbox(userId);
}

internal sealed class PersistedData
{
    public PersistedUsers Users { get; set; } = new();
    public PersistedRegistry Registry { get; set; } = new();
    public PersistedNotifications Notifications { get; set; } = new();
}

internal sealed class PersistedUsers
{
    public Dictionary<string, Customer> Customers { get; set; } = new();
    public Dictionary<string, User> Staff { get; set; } = new();
    public List<Availability> Availabilities { get; set; } = new();
}

internal sealed class PersistedRegistry
{
    public int Counter { get; set; }
    public Dictionary<string, DeliveryRequest> Requests { get; set; } = new();
}

internal sealed class PersistedNotifications
{
    public Dictionary<string, List<NotificationMessage>> Inboxes { get; set; } = new();
}