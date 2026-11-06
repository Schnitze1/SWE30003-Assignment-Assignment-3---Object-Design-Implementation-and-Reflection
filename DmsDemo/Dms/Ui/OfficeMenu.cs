namespace DmsDemo.Dms.Ui;

public sealed class OfficeMenu : UiBase
{
    public OfficeMenu(DeliveryManagementSystem dms) : base(dms) { }

    public void Show()
    {
        Console.WriteLine("\nOffice staff (choose staff ID)");
        var staffId = Prompt("Enter staff ID (e.g. O-001): ").ToUpperInvariant();
        if (string.IsNullOrEmpty(staffId)) return;
        var staff = Dms.Users.FindStaff(staffId);
        if (staff is not OfficeStaff)
        { Error($"Office staff ID '{staffId}' not found."); return; }

        while (Running)
        {
            Console.WriteLine($"\nOffice staff ({staff.Id} - {staff.Name}):");
            Console.WriteLine("  1 All requests (filter by status)   2 Issue quote (standard or express, optional override)");
            Console.WriteLine("  3 Allocate driver   4 Reassign driver");
            Console.WriteLine("  5 Handle failed pickup or delivery   6 Cancel request   7 Inbox   0 Back");
            var choice = Prompt("Choice: ");
            switch (choice)
            {
                case "1": AllRequests(); break;
                case "2": IssueQuote(); break;
                case "3": AllocateDriver(); break;
                case "4": ReassignDriver(); break;
                case "5": HandleFailed(); break;
                case "6": CancelRequest(staff.Id); break;
                case "7": ShowInbox(staff.Id); break;
                case "0": return;
                default: Error("Invalid selection."); break;
            }
        }
    }

    private void AllRequests()
    {
        var filter = Prompt("Filter by status (press Enter for all): ");
        var reqs = Dms.ListRequests(state: string.IsNullOrEmpty(filter) ? null : filter);
        Console.WriteLine($"\nRequests ({reqs.Count} found):");
        Console.WriteLine(new string('-', 75));
        Console.WriteLine($"{"Ref",-10} {"Status",-14} {"Customer",-12} {"Pickup",-12} {"Driver",-10} Parcels");
        Console.WriteLine(new string('-', 75));
        foreach (var r in reqs)
        {
            var driver = r.CurrentAssignment()?.DriverId ?? "-";
            Console.WriteLine($"{r.Ref,-10} {r.State.Name,-14} {r.CustomerPhone,-12} " +
                $"{r.PickupDate,-12} {driver,-10} {r.Parcels.Count}");
        }
        Console.WriteLine(new string('-', 75));
    }

    private void IssueQuote()
    {
        var @ref = Prompt("Enter request reference: ").ToUpperInvariant();
        if (string.IsNullOrEmpty(@ref)) return;
        try
        {
            var req = Dms.GetRequest(@ref);
            if (req.State.Name is not ("Requested" or "Quoted"))
            {
                Error($"Cannot quote request {@ref} currently in '{req.State.Name}'.");
                return;
            }
            Console.WriteLine($"\nRequest {req.Ref} details:");
            Console.WriteLine($"  Parcels: {req.Parcels.Count} | Total Weight: {req.TotalWeightKg()}kg");
            var std = Dms.StandardPricing.Calculate(req.Parcels);
            var exp = Dms.ExpressPricing.Calculate(req.Parcels);
            Console.WriteLine($"  Standard pricing estimate: ${std:F2}");
            Console.WriteLine($"  Express pricing estimate : ${exp:F2}");

            var stratIn = Prompt("Select strategy (1 Standard, 2 Express): ");
            var strat = stratIn == "2" ? "express" : "standard";

            double? ovrAmt = null;
            string? ovrReason = null;
            if (Prompt("Apply price override? (y/n): ").ToLowerInvariant() == "y")
            {
                if (double.TryParse(Prompt("Enter override price ($): "), out var a))
                    ovrAmt = a;
                ovrReason = Prompt("Enter reason for override (>= 5 chars): ");
            }

            var q = Dms.GenerateQuote(@ref, strat, ovrAmt, ovrReason);
            Success($"Quote of ${q.Amount:F2} ({q.Strategy}) issued for {@ref}.");
        }
        catch (DmsException e) { Error(e.Message); }
    }

    private void AllocateDriver()
    {
        var @ref = Prompt("Enter accepted request reference: ").ToUpperInvariant();
        if (string.IsNullOrEmpty(@ref)) return;
        try
        {
            var eligible = Dms.ListEligibleDrivers(@ref);
            Console.WriteLine($"\nEligible drivers for {@ref} ({eligible.Count} available):");
            foreach (var d in eligible)
                Console.WriteLine($"  Driver {d.Id} ({d.Name}) - Active jobs: {Dms.Allocator.GetActiveJobCount(d.Id)}");

            var driverIdIn = Prompt("Enter Driver ID (Enter for least loaded): ").ToUpperInvariant();
            var allocated = Dms.AllocateDriver(@ref,
                string.IsNullOrEmpty(driverIdIn) ? null : driverIdIn);
            Success($"Driver {allocated.Id} ({allocated.Name}) allocated to request {@ref}.");
        }
        catch (DmsException e) { Error(e.Message); }
    }

    private void ReassignDriver()
    {
        var @ref = Prompt("Enter assigned request reference: ").ToUpperInvariant();
        if (string.IsNullOrEmpty(@ref)) return;
        try
        {
            var req = Dms.GetRequest(@ref);
            var current = req.CurrentAssignment()?.DriverId ?? "None";
            Console.WriteLine($"Current driver: {current}");
            var eligible = Dms.ListEligibleDrivers(@ref);
            Console.WriteLine("Eligible alternative drivers:");
            foreach (var d in eligible.Where(d => d.Id != current))
                Console.WriteLine($"  {d.Id} ({d.Name}) - Active jobs: {Dms.Allocator.GetActiveJobCount(d.Id)}");

            var newId = Prompt("Enter new Driver ID: ").ToUpperInvariant();
            var reason = Prompt("Enter reassignment reason: ");
            var allocated = Dms.ReassignDriver(@ref, newId, reason);
            Success($"Request {@ref} successfully reassigned to driver {allocated.Id}.");
        }
        catch (DmsException e) { Error(e.Message); }
    }

    private void HandleFailed()
    {
        var failed = Dms.ListRequests()
            .Where(r => r.State.Name is "PickupFailed" or "DeliveryFailed").ToList();
        if (failed.Count == 0) { Console.WriteLine("\nNo failed requests.\n"); return; }
        Console.WriteLine("\nFailed Requests:");
        foreach (var r in failed) Console.WriteLine($"  {r.Ref} - Status: {r.State.Name}");

        var @ref = Prompt("Enter reference to handle: ").ToUpperInvariant();
        try
        {
            var req = Dms.GetRequest(@ref);
            if (req.State.Name == "PickupFailed")
            {
                Console.WriteLine("\nOptions: 1 Reallocate driver   2 Cancel request   0 Back");
                var opt = Prompt("Option: ");
                if (opt == "1")
                {
                    var dIn = Prompt("Enter Driver ID (Enter for least loaded): ").ToUpperInvariant();
                    var drv = Dms.ReallocateAfterFailure(@ref,
                        string.IsNullOrEmpty(dIn) ? null : dIn);
                    Success($"Reallocated to driver {drv.Id}. Request is Assigned.");
                }
                else if (opt == "2")
                {
                    var rsn = Prompt("Cancellation reason: ");
                    Dms.CancelRequest(@ref, "office", rsn);
                    Success($"Request {@ref} cancelled.");
                }
            }
            else if (req.State.Name == "DeliveryFailed")
            {
                Console.WriteLine("\nOptions: 1 Retry delivery   2 Cancel request   0 Back");
                var opt = Prompt("Option: ");
                if (opt == "1")
                {
                    Dms.RetryDelivery(@ref);
                    Success("Delivery retry authorized. Request is PickedUp.");
                }
                else if (opt == "2")
                {
                    var rsn = Prompt("Cancellation reason: ");
                    Dms.CancelRequest(@ref, "office", rsn);
                    Success($"Request {@ref} cancelled.");
                }
            }
            else Error($"Request {@ref} is in '{req.State.Name}'.");
        }
        catch (DmsException e) { Error(e.Message); }
    }

    private void CancelRequest(string staffId)
    {
        var @ref = Prompt("Enter reference to cancel: ").ToUpperInvariant();
        if (string.IsNullOrEmpty(@ref)) return;
        var reason = Prompt("Reason for cancellation: ");
        try { Dms.CancelRequest(@ref, staffId, reason); Success($"Request {@ref} cancelled."); }
        catch (DmsException e) { Error(e.Message); }
    }
}