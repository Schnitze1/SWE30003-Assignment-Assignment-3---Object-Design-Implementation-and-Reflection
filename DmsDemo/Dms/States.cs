namespace DmsDemo.Dms;

public abstract class RequestState
{
    public abstract string Name { get; }

    protected InvalidTransitionException Illegal(string action) =>
        new($"Cannot {action} while in '{Name}' state.");

    public virtual RequestState IssueQuote(string actor) => throw Illegal("issue quote");
    public virtual RequestState AcceptQuote(string actor) => throw Illegal("accept quote");
    public virtual RequestState RejectQuote(string actor) => throw Illegal("reject quote");
    public virtual RequestState Requote(string actor) => throw Illegal("re-quote");
    public virtual RequestState Allocate(string actor) => throw Illegal("allocate driver");
    public virtual RequestState Reassign(string actor) => throw Illegal("reassign driver");
    public virtual RequestState PickupDone(string actor) => throw Illegal("complete pickup");
    public virtual RequestState PickupFailed(string actor) => throw Illegal("record pickup failure");
    public virtual RequestState ParcelMismatch(string actor) => throw Illegal("record parcel mismatch");
    public virtual RequestState Reallocate(string actor) => throw Illegal("reallocate driver");
    public virtual RequestState DeliverSuccess(string actor) => throw Illegal("record delivery success");
    public virtual RequestState DeliverFailed(string actor) => throw Illegal("record delivery failure");
    public virtual RequestState RetryDelivery(string actor) => throw Illegal("retry delivery");
    public virtual RequestState Cancel(string actor, bool isOffice) => throw Illegal("cancel");

    public static RequestState FromName(string name) => name switch
    {
        "Requested" => new RequestedState(),
        "Quoted" => new QuotedState(),
        "Accepted" => new AcceptedState(),
        "Assigned" => new AssignedState(),
        "PickupFailed" => new PickupFailedState(),
        "PickedUp" => new PickedUpState(),
        "DeliveryFailed" => new DeliveryFailedState(),
        "Delivered" => new DeliveredState(),
        "Rejected" => new RejectedState(),
        "Cancelled" => new CancelledState(),
        _ => throw new ArgumentException($"Unknown request state name: {name}"),
    };
}

public sealed class RequestedState : RequestState
{
    public override string Name => "Requested";
    public override RequestState IssueQuote(string actor) => new QuotedState();
    public override RequestState Cancel(string actor, bool isOffice) => new CancelledState();
}

public sealed class QuotedState : RequestState
{
    public override string Name => "Quoted";
    public override RequestState AcceptQuote(string actor) => new AcceptedState();
    public override RequestState RejectQuote(string actor) => new RejectedState();
    public override RequestState Requote(string actor) => new RequestedState();
    public override RequestState Cancel(string actor, bool isOffice) => new CancelledState();
}

public sealed class AcceptedState : RequestState
{
    public override string Name => "Accepted";
    public override RequestState Allocate(string actor) => new AssignedState();
    public override RequestState Cancel(string actor, bool isOffice) => new CancelledState();
}

public sealed class AssignedState : RequestState
{
    public override string Name => "Assigned";
    public override RequestState Reassign(string actor) => new AssignedState();
    public override RequestState PickupDone(string actor) => new PickedUpState();
    public override RequestState PickupFailed(string actor) => new PickupFailedState();
    public override RequestState ParcelMismatch(string actor) => new RequestedState();
    public override RequestState Cancel(string actor, bool isOffice) => new CancelledState();
}

public sealed class PickupFailedState : RequestState
{
    public override string Name => "PickupFailed";
    public override RequestState Reallocate(string actor) => new AssignedState();
    public override RequestState Cancel(string actor, bool isOffice) =>
        isOffice ? new CancelledState()
                 : throw new InvalidTransitionException(
                     "Only office staff can cancel after a failed pickup.");
}

public sealed class PickedUpState : RequestState
{
    public override string Name => "PickedUp";
    public override RequestState DeliverSuccess(string actor) => new DeliveredState();
    public override RequestState DeliverFailed(string actor) => new DeliveryFailedState();
}

public sealed class DeliveryFailedState : RequestState
{
    public override string Name => "DeliveryFailed";
    public override RequestState RetryDelivery(string actor) => new PickedUpState();
    public override RequestState Cancel(string actor, bool isOffice) =>
        isOffice ? new CancelledState()
                 : throw new InvalidTransitionException(
                     "Only office staff can cancel after a failed delivery.");
}

public sealed class DeliveredState : RequestState
{
    public override string Name => "Delivered";
}

public sealed class RejectedState : RequestState
{
    public override string Name => "Rejected";
}

public sealed class CancelledState : RequestState
{
    public override string Name => "Cancelled";
}