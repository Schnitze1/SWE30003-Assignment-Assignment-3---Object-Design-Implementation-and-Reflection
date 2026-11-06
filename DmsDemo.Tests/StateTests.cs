using DmsDemo.Dms;
using Xunit;

namespace DmsDemo.Tests;

public class StateTests
{
    [Fact]
    public void Happy_Path()
    {
        RequestState s = new RequestedState();
        s = s.IssueQuote("office");
        Assert.IsType<QuotedState>(s);
        s = s.AcceptQuote("0400000001");
        Assert.IsType<AcceptedState>(s);
        s = s.Allocate("office");
        Assert.IsType<AssignedState>(s);
        s = s.PickupDone("D-001");
        Assert.IsType<PickedUpState>(s);
        s = s.DeliverSuccess("D-001");
        Assert.IsType<DeliveredState>(s);
    }

    [Fact]
    public void Cancel_Permissions()
    {
        Assert.IsType<CancelledState>(new RequestedState().Cancel("c", false));
        Assert.IsType<CancelledState>(new QuotedState().Cancel("c", false));
        Assert.IsType<CancelledState>(new AcceptedState().Cancel("c", false));
        Assert.IsType<CancelledState>(new AssignedState().Cancel("c", false));
        Assert.Throws<InvalidTransitionException>(() => new PickupFailedState().Cancel("c", false));
        Assert.Throws<InvalidTransitionException>(() => new DeliveryFailedState().Cancel("c", false));
    }

    [Fact]
    public void Illegal_Transitions()
    {
        Assert.Throws<InvalidTransitionException>(() => new RequestedState().AcceptQuote("c"));
        Assert.Throws<InvalidTransitionException>(() => new RequestedState().Allocate("office"));
        Assert.Throws<InvalidTransitionException>(() => new DeliveredState().Cancel("office", true));
    }
}