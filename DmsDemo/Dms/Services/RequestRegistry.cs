namespace DmsDemo.Dms.Services;

public sealed class RequestRegistry
{
    public Dictionary<string, DeliveryRequest> Requests { get; private set; } = new();
    public int Counter { get; set; } = 1;
    private readonly List<IStatusObserver> _observers = new();

    public void AttachObserver(IStatusObserver observer)
    {
        if (!_observers.Contains(observer)) _observers.Add(observer);
        foreach (var req in Requests.Values) req.AttachObserver(observer);
    }

    public string NextRef() => $"YOW-{Counter++:D4}";

    public void AddRequest(DeliveryRequest request)
    {
        foreach (var obs in _observers) request.AttachObserver(obs);
        Requests[request.Ref] = request;
    }

    public DeliveryRequest GetRequest(string @ref)
    {
        var clean = @ref.Trim().ToUpperInvariant();
        return Requests.TryGetValue(clean, out var req)
            ? req
            : throw new NotFoundException($"Delivery request '{@ref}' not found.");
    }

    public List<DeliveryRequest> ListRequests(string? phone = null, string? state = null)
    {
        IEnumerable<DeliveryRequest> q = Requests.Values;
        if (!string.IsNullOrEmpty(phone))
        {
            var clean = phone.Replace(" ", "").Trim();
            q = q.Where(r => r.CustomerPhone == clean);
        }
        if (!string.IsNullOrEmpty(state))
            q = q.Where(r => string.Equals(r.State.Name, state,
                StringComparison.OrdinalIgnoreCase));
        return q.ToList();
    }

    public List<StatusRecord> Track(string @ref) =>
        GetRequest(@ref).History.ToList();

    public void Restore(Dictionary<string, DeliveryRequest> requests, int counter)
    {
        Requests = requests;
        Counter = counter;
    }
}