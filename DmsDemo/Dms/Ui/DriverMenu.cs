namespace DmsDemo.Dms.Ui;

public sealed class DriverMenu : UiBase
{
    public DriverMenu(DeliveryManagementSystem dms) : base(dms) { }

    public void Show()
    {
        Console.WriteLine("\nDelivery staff (choose driver ID)");
        var driverId = Prompt("Enter driver ID (e.g. D-001): ").ToUpperInvariant();
        if (string.IsNullOrEmpty(driverId)) return;
        var staff = Dms.Users.FindStaff(driverId);
        if (staff is not DeliveryStaff)
        { Error($"Delivery driver '{driverId}' not found."); return; }

        while (Running)
        {
            Console.WriteLine($"\nDelivery staff ({staff.Id} - {staff.Name}):");
            Console.WriteLine("  1 My jobs   2 Record pickup (match check, payment)   3 Record delivery (proof)");
            Console.WriteLine("  4 Set or remove availability   5 Inbox   0 Back");
            var choice = Prompt("Choice: ");
            switch (choice)
            {
                case "1": MyJobs(staff.Id); break;
                case "2": RecordPickup(staff.Id); break;
                case "3": RecordDelivery(staff.Id); break;
                case "4": ManageAvailability(staff.Id); break;
                case "5": ShowInbox(staff.Id); break;
                case "0": return;
                default: Error("Invalid selection."); break;
            }
        }
    }

    private void MyJobs(string driverId)
    {
        var jobs = Dms.ListRequests()
            .Where(r => r.CurrentAssignment() is { Active: true } a && a.DriverId == driverId)
            .ToList();
        Console.WriteLine($"\nActive Jobs for Driver {driverId} ({jobs.Count} assigned):");
        if (jobs.Count == 0) { Console.WriteLine("No active jobs assigned."); return; }
        Console.WriteLine(new string('-', 75));
        Console.WriteLine($"{"Ref",-10} {"State",-14} {"Pickup",-12} {"Parcels",-8} Pickup Suburb");
        Console.WriteLine(new string('-', 75));
        foreach (var j in jobs)
            Console.WriteLine($"{j.Ref,-10} {j.State.Name,-14} {j.PickupDate,-12} {j.Parcels.Count,-8} {j.PickupAddress.Suburb}");
        Console.WriteLine(new string('-', 75));
    }

    private void RecordPickup(string driverId)
    {
        var @ref = Prompt("Enter request reference to pickup: ").ToUpperInvariant();
        if (string.IsNullOrEmpty(@ref)) return;
        try
        {
            var req = Dms.GetRequest(@ref);
            if (req.State.Name != "Assigned")
            { Error($"Request {@ref} is in '{req.State.Name}', not Assigned."); return; }
            if (req.CurrentAssignment()?.DriverId != driverId)
            { Error($"Request {@ref} is not assigned to you."); return; }

            Console.WriteLine("\nParcel Manifest:");
            for (var i = 0; i < req.Parcels.Count; i++)
                Console.WriteLine($"  [{i + 1}] {req.Parcels[i].Spec.Description} ({req.Parcels[i].Spec.WeightKg}kg)");

            // ---- CHANGED: split "no" into mismatch vs nobody-at-door ----
            if (Prompt("Do parcels match description? (y/n): ").ToLowerInvariant() != "y")
            {
                Console.WriteLine("  1 Wrong parcels (mismatch)   2 Nobody at door (pickup failed)");
                var kind = Prompt("Select option: ");

                if (kind == "2")
                {
                    var reason = Prompt("Reason for failed pickup: ");
                    Dms.RecordPickupFailed(@ref, driverId, reason);
                    Success($"Pickup failed for {@ref}. Request is PickupFailed. Office notified.");
                    return;
                }

                // default to mismatch (covers "1" and any other input)
                var note = Prompt("Enter parcel mismatch details: ");
                Dms.RecordPickup(@ref, driverId, false, note: note);
                Success($"Parcel mismatch flagged. Request {@ref} returned to Requested.");
                return;
            }

            var q = req.CurrentQuote();
            var amt = q?.Amount ?? 0.0;
            Console.WriteLine($"\nPayment due: ${amt:F2}");
            Console.WriteLine("Payment methods: 1 Cash   2 Card (simulated)   3 Unpaid");
            var method = Prompt("Select payment method: ") switch
            {
                "1" => "cash",
                "2" => "card",
                _ => "unpaid"
            };
            var pNote = Prompt("Optional pickup note: ");
            Dms.RecordPickup(@ref, driverId, true, method, pNote);
            Success($"Pickup completed! Request {@ref} is now PickedUp.");
        }
        catch (DmsException e) { Error(e.Message); }
    }

    private void RecordDelivery(string driverId)
    {
        var @ref = Prompt("Enter request reference to deliver: ").ToUpperInvariant();
        if (string.IsNullOrEmpty(@ref)) return;
        try
        {
            var req = Dms.GetRequest(@ref);
            if (req.State.Name != "PickedUp")
            { Error($"Request {@ref} is in '{req.State.Name}', not PickedUp."); return; }
            if (req.CurrentAssignment()?.DriverId != driverId)
            { Error($"Request {@ref} is not assigned to you."); return; }

            Console.WriteLine("\nDelivery outcome: 1 Successful with proof   2 Failed (absent or refused)   0 Back");
            var outcome = Prompt("Outcome: ");
            if (outcome == "1")
            {
                var receivedBy = Prompt("Received by (full name): ");
                var proof = Dms.RecordDelivery(@ref, driverId, receivedBy);
                Success($"Delivery completed for {@ref}! Proof recorded: {proof.ReceivedBy}");
            }
            else if (outcome == "2")
            {
                var reason = Prompt("Reason for delivery failure: ");
                Dms.RecordDeliveryFailed(@ref, driverId, reason);
                Success($"Delivery marked as failed for {@ref}. Office alerted.");
            }
        }
        catch (DmsException e) { Error(e.Message); }
    }

    private void ManageAvailability(string driverId)
    {
        var avails = Dms.Users.GetDriverAvailabilities(driverId);
        Console.WriteLine($"\nCurrent Availability Windows ({avails.Count} shifts):");
        for (var i = 0; i < avails.Count; i++)
            Console.WriteLine($"  [{i}] {avails[i].Period.Start:yyyy-MM-dd HH:mm} to {avails[i].Period.End:yyyy-MM-dd HH:mm}");
        Console.WriteLine("\nOptions: 1 Add availability   2 Remove availability   0 Back");
        var opt = Prompt("Option: ");
        if (opt == "1")
        {
            try
            {
                var s = DateTime.ParseExact(Prompt("Start datetime (YYYY-MM-DD HH:MM): ").Trim(),
                    "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                var e = DateTime.ParseExact(Prompt("End datetime   (YYYY-MM-DD HH:MM): ").Trim(),
                    "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                Dms.SetAvailability(driverId, s, e);
                Success($"Availability added for driver {driverId}.");
            }
            catch (FormatException) { Error("Invalid date format."); }
            catch (DmsException e) { Error(e.Message); }
        }
        else if (opt == "2")
        {
            try
            {
                var idx = int.Parse(Prompt("Enter shift index to remove: "));
                Dms.RemoveAvailability(driverId, idx);
                Success($"Shift index {idx} removed.");
            }
            catch (FormatException) { Error("Invalid index."); }
            catch (DmsException e) { Error(e.Message); }
        }
    }
}