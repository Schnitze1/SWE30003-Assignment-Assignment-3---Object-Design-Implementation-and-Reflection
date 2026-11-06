using System.Text.Json.Serialization;

namespace DmsDemo.Dms;

public sealed class Parcel
{
    public ParcelSpecification Spec { get; set; } = new("", 0, 0, 0, 0);
    public bool Mismatch { get; set; }
    public string MismatchNote { get; set; } = "";

    public Parcel() { }

    public Parcel(ParcelSpecification spec, bool mismatch = false, string mismatchNote = "")
    {
        Spec = spec;
        Mismatch = mismatch;
        MismatchNote = mismatchNote;
    }
}

public sealed class Quote
{
    public double Amount { get; set; }
    public string Strategy { get; set; } = "";
    public string Status { get; set; } = "open";
    public string OverrideReason { get; set; } = "";

    public Quote() { }

    public Quote(double amount, string strategy, string status = "open", string overrideReason = "")
    {
        Amount = amount;
        Strategy = strategy;
        Status = status;
        OverrideReason = overrideReason;
    }
}

public sealed class DeliveryAssignment
{
    public string DriverId { get; set; } = "";
    public string AssignedAt { get; set; } = "";
    public bool Active { get; set; } = true;
    public string ReassignmentReason { get; set; } = "";

    public DeliveryAssignment() { }

    public DeliveryAssignment(string driverId, string assignedAt, bool active = true, string reason = "")
    {
        DriverId = driverId;
        AssignedAt = assignedAt;
        Active = active;
        ReassignmentReason = reason;
    }
}

public sealed class DeliveryRequest
{
    public string Ref { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public ContactDetails SenderContact { get; set; } = new("", "");
    public Address PickupAddress { get; set; } = new("", "", "VIC", "");
    public string PickupDate { get; set; } = "";
    public string PickupTime { get; set; } = "";
    public Recipient Recipient { get; set; } =
        new(new ContactDetails("", ""), new Address("", "", "VIC", ""));
    public List<Parcel> Parcels { get; set; } = new();
    public List<Quote> Quotes { get; set; } = new();
    public List<DeliveryAssignment> Assignments { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public ProofOfDelivery? Proof { get; set; }
    public List<StatusRecord> History { get; set; } = new();

    [JsonIgnore]
    private readonly List<IStatusObserver> _observers = new();

    [JsonIgnore]
    public RequestState State { get; set; } = new RequestedState();

    [JsonPropertyName("state")]
    public string StateName
    {
        get => State.Name;
        set => State = RequestState.FromName(value);
    }

    public DeliveryRequest() { }

    public DeliveryRequest(
        string @ref, string customerPhone,
        ContactDetails senderContact, Address pickupAddress,
        string pickupDate, string pickupTime,
        Recipient recipient, List<Parcel> parcels,
        RequestState? state = null)
    {
        Ref = @ref;
        CustomerPhone = customerPhone;
        SenderContact = senderContact;
        PickupAddress = pickupAddress;
        PickupDate = pickupDate;
        PickupTime = pickupTime;
        Recipient = recipient;
        Parcels = parcels;
        State = state ?? new RequestedState();
    }

    public void AttachObserver(IStatusObserver observer)
    {
        if (!_observers.Contains(observer)) _observers.Add(observer);
    }

    public void DetachObserver(IStatusObserver observer) =>
        _observers.Remove(observer);

    public void TransitionTo(RequestState newState, string actor, string note = "")
    {
        State = newState;
        var record = new StatusRecord(
            newState.Name,
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            actor, note);
        History.Add(record);
        foreach (var o in _observers.ToList()) o.OnStatusChange(this, record);
    }

    public void AddQuote(Quote quote)
    {
        foreach (var q in Quotes.Where(q => q.Status == "open"))
            q.Status = "replaced";
        Quotes.Add(quote);
    }

    public Quote? CurrentQuote() =>
        Quotes.LastOrDefault(q => q.Status == "open")
        ?? Quotes.LastOrDefault();

    public DeliveryAssignment? CurrentAssignment() =>
        Assignments.LastOrDefault(a => a.Active);

    public double TotalWeightKg() => Parcels.Sum(p => p.Spec.WeightKg);

    public IReadOnlyList<IStatusObserver> GetObservers() => _observers;
}