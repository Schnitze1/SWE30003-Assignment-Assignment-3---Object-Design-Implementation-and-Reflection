using DmsDemo.Dms;
using DmsDemo.Dms.Services;
using Xunit;

namespace DmsDemo.Tests;

public class ScenarioTests : IDisposable
{
    private readonly string _path;
    private readonly DeliveryManagementSystem _dms;
    private readonly string _today;

    public ScenarioTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"dms_test_{Guid.NewGuid():N}.json");
        _dms = new DeliveryManagementSystem(new JsonStore(_path), reset: true);
        _today = DateTime.Today.ToString("yyyy-MM-dd");
    }

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
    }

    private RequestInput MakeInput(string pTime) => new(
        new AddressInput("100 Flinders St", "Melbourne", "3000"),
        _today, pTime,
        new RecipientInput("Jane Doe", "0411222333", "jane@example.com",
            "50 Lygon St", "Carlton", "3053"),
        new List<ParcelInput> { new("Art supplies", 2.0, 25.0, 20.0, 15.0) });

    private DeliveryRequest SetupAssigned(string time)
    {
        var r = _dms.SubmitRequest("0400000001", MakeInput(time));
        _dms.GenerateQuote(r.Ref, "standard");
        _dms.RespondToQuote(r.Ref, true);
        _dms.AllocateDriver(r.Ref, "D-001");
        return r;
    }

    [Fact]
    public void S1_Invalid_Then_Valid()
    {
        var bad = new RequestInput(
            new AddressInput("", "Melbourne", "3000"),
            _today, "10:00",
            new RecipientInput("Bob", "0400000002", "", "20 Main St", "Carlton", "3053"),
            new List<ParcelInput> { new("Item", -1, 10, 10, 10) });
        Assert.Throws<ValidationException>(() => _dms.SubmitRequest("0400000001", bad));

        var req = _dms.SubmitRequest("0400000001", MakeInput("11:00"));
        Assert.StartsWith("YOW-", req.Ref);
        Assert.Equal("Requested", req.State.Name);
        Assert.Contains(_dms.Inbox("office"),
            m => m.Title == "New Delivery Request Logged");
    }

    [Fact]
    public void S2_Accept_Then_Reject()
    {
        var r1 = _dms.SubmitRequest("0400000001", MakeInput("10:00"));
        var q1 = _dms.GenerateQuote(r1.Ref, "standard");
        Assert.Equal(15.00, q1.Amount);
        Assert.Equal("Quoted", r1.State.Name);
        _dms.RespondToQuote(r1.Ref, true);
        Assert.Equal("Accepted", r1.State.Name);

        var r2 = _dms.SubmitRequest("0400000001", MakeInput("12:00"));
        var q2 = _dms.GenerateQuote(r2.Ref, "express", 30, "Express VIP priority");
        Assert.Equal(30, q2.Amount);
        _dms.RespondToQuote(r2.Ref, false);
        Assert.Equal("Rejected", r2.State.Name);
    }

    [Fact]
    public void S3_Allocate_Reassign_NoDriver()
    {
        var r = _dms.SubmitRequest("0400000001", MakeInput("14:00"));
        _dms.GenerateQuote(r.Ref, "standard");
        _dms.RespondToQuote(r.Ref, true);

        var d = _dms.AllocateDriver(r.Ref);
        Assert.Equal("D-001", d.Id);
        Assert.Equal("Assigned", r.State.Name);

        var d2 = _dms.ReassignDriver(r.Ref, "D-002", "Route optimization");
        Assert.Equal("D-002", d2.Id);

        var farDate = DateTime.Today.AddDays(20).ToString("yyyy-MM-dd");
        var farInput = MakeInput("10:00") with { PickupDate = farDate };
        var rf = _dms.SubmitRequest("0400000001", farInput);
        _dms.GenerateQuote(rf.Ref, "standard");
        _dms.RespondToQuote(rf.Ref, true);
        Assert.Throws<NoDriverAvailableException>(() => _dms.AllocateDriver(rf.Ref));

        _dms.SetAvailability("D-001",
            DateTime.Today.AddDays(20).AddHours(8), DateTime.Today.AddDays(20).AddHours(18));
        var drv = _dms.AllocateDriver(rf.Ref);
        Assert.Equal("D-001", drv.Id);
    }

    [Fact]
    public void S4_Pickup_Delivery_Failures()
    {
        var r1 = SetupAssigned("10:00");
        var pay = _dms.RecordPickup(r1.Ref, "D-001", true, "cash");
        Assert.Equal("completed", pay!.Status);
        Assert.Equal("PickedUp", r1.State.Name);
        var proof = _dms.RecordDelivery(r1.Ref, "D-001", "Bob Boss");
        Assert.Equal("Bob Boss", proof.ReceivedBy);
        Assert.Equal("Delivered", r1.State.Name);

        var r2 = SetupAssigned("11:00");
        var pay2 = _dms.RecordPickup(r2.Ref, "D-001", true, "unpaid");
        Assert.Equal("unpaid", pay2!.Status);
        Assert.Contains(_dms.Inbox("office"), m => m.Title == "Unpaid Delivery at Pickup");

        var r3 = SetupAssigned("12:00");
        _dms.RecordPickupFailed(r3.Ref, "D-001", "No answer at door");
        Assert.Equal("PickupFailed", r3.State.Name);
        _dms.ReallocateAfterFailure(r3.Ref, "D-002");
        Assert.Equal("Assigned", r3.State.Name);

        var r4 = SetupAssigned("13:00");
        _dms.RecordPickup(r4.Ref, "D-001", true, "card");
        _dms.RecordDeliveryFailed(r4.Ref, "D-001", "Refused");
        Assert.Equal("DeliveryFailed", r4.State.Name);
        _dms.RetryDelivery(r4.Ref);
        Assert.Equal("PickedUp", r4.State.Name);
    }

    [Fact]
    public void S5_Cancellations()
    {
        var r = _dms.SubmitRequest("0400000001", MakeInput("15:00"));
        _dms.GenerateQuote(r.Ref, "standard");
        _dms.CancelRequest(r.Ref, "0400000001", "Found alternate courier");
        Assert.Equal("Cancelled", r.State.Name);

        var r2 = SetupAssigned("16:00");
        _dms.RecordPickup(r2.Ref, "D-001", false, note: "Parcel mismatch");
        Assert.Equal("Requested", r2.State.Name);
        Assert.True(r2.Parcels[0].Mismatch);
    }

    [Fact]
    public void S6_Track_And_Persistence()
    {
        var r = SetupAssigned("17:00");
        var history = _dms.Track(r.Ref);
        Assert.True(history.Count >= 3);

        Assert.Throws<NotFoundException>(() => _dms.Track("YOW-9999"));

        var reloaded = new DeliveryManagementSystem(new JsonStore(_path));
        var reloadedReq = reloaded.GetRequest(r.Ref);
        Assert.Equal("Assigned", reloadedReq.State.Name);
    }
}