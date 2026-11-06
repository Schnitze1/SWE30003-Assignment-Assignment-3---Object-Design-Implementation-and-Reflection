namespace DmsDemo.Dms.Ui;

public sealed class ConsoleUi : UiBase
{
    public ConsoleUi(DeliveryManagementSystem dms) : base(dms) { }

    public void Run()
    {
        Running = true;
        while (Running)
        {
            Console.WriteLine(
                "HOME: 1 Customer  2 Office staff  3 Delivery staff  " +
                "4 Track a delivery  0 Exit");
            var choice = Prompt("Enter choice: ");
            switch (choice)
            {
                case "1": new CustomerMenu(Dms).Show(); break;
                case "2": new OfficeMenu(Dms).Show(); break;
                case "3": new DriverMenu(Dms).Show(); break;
                case "4": TrackDelivery(); break;
                case "0":
                    Console.WriteLine(
                        "\nThank you for using Your Order We Deliver. Goodbye!\n");
                    return;
                default:
                    Error("Invalid selection. Please enter 0, 1, 2, 3 or 4.");
                    break;
            }
        }
    }

    private void TrackDelivery()
    {
        Console.WriteLine("\n Track a delivery");
        Console.WriteLine(
            "Track: enter reference, show status timeline. " +
            "Unknown reference gives an error.");
        var @ref = Prompt("Enter reference (e.g. YOW-0001): ");
        if (string.IsNullOrEmpty(@ref)) return;

        try
        {
            var req = Dms.GetRequest(@ref);
            var history = Dms.Track(@ref);
            Console.WriteLine($"\nTracking Delivery Request: {req.Ref}");
            Console.WriteLine($"Current Status : {req.State.Name}");
            Console.WriteLine($"Customer Phone : {req.CustomerPhone}");
            var pa = req.PickupAddress;
            Console.WriteLine($"Pickup Address : {pa.Street}, {pa.Suburb} VIC {pa.Postcode}");
            var ra = req.Recipient.Address;
            Console.WriteLine(
                $"Delivery To    : {req.Recipient.Contact.Name}, " +
                $"{ra.Street}, {ra.Suburb} VIC {ra.Postcode}");
            Console.WriteLine("\nStatus Timeline:");
            Console.WriteLine($"{"Timestamp",-20} {"State",-15} {"Actor",-12} Note");
            foreach (var rec in history)
                Console.WriteLine(
                    $"{rec.Timestamp,-20} {rec.StateName,-15} {rec.Actor,-12} {rec.Note}");
        }
        catch (DmsException e) { Error(e.Message); }
    }
}