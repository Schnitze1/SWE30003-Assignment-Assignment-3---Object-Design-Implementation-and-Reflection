using DmsDemo.Dms;
using DmsDemo.Dms.Services;

namespace DmsDemo;

public static class RunDemo
{
    public static void Execute()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"dms_demo_{Guid.NewGuid():N}.json");
        var store = new JsonStore(tmp);
        var dms = new DeliveryManagementSystem(store, reset: true);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        Console.WriteLine(new string('=', 70));
        Console.WriteLine("DEMO EXECUTION: DMS 6 SCENARIOS (SWE30003 Group 7)");
        Console.WriteLine(new string('=', 70));

        var input = new RequestInput(
            new AddressInput("120 Collins St", "Melbourne", "3000"),
            today, "09:30",
            new RecipientInput("Sarah Connor", "0412345678", "sarah@cyberdyne.com",
                "250 Elizabeth St", "Melbourne", "3000"),
            new List<ParcelInput>
            {
                new("Component Box A", 3.5, 30.0, 20.0, 15.0),
                new("Component Box B", 2.5, 25.0, 15.0, 10.0),
            });

        Console.WriteLine("\n>>> SCENARIO S1: Log a Request (Validation, Issuance)");
        var s1 = dms.SubmitRequest("0400000001", input);
        Console.WriteLine($"[S1 Success] Tracking reference: {s1.Ref}");
        Console.WriteLine($"[S1 Status ] State: {s1.State.Name} | Parcels: {s1.Parcels.Count} | Weight: {s1.TotalWeightKg()}kg");

        Console.WriteLine("\n>>> SCENARIO S2: Quote, Accept or Reject");
        var q1 = dms.GenerateQuote(s1.Ref, "standard");
        Console.WriteLine($"[S2 Quote 1] {s1.Ref}: {q1.Strategy} price ${q1.Amount:F2}");
        dms.RespondToQuote(s1.Ref, true);
        Console.WriteLine($"[S2 Accept ] Customer accepted quote. State: {s1.State.Name}");

        var s2 = dms.SubmitRequest("0400000001", input);
        var q2 = dms.GenerateQuote(s2.Ref, "express", 45.00, "Express handling fee");
        Console.WriteLine($"[S2 Quote 2] {s2.Ref}: Express override ${q2.Amount:F2} ({q2.OverrideReason})");
        dms.RespondToQuote(s2.Ref, false);
        Console.WriteLine($"[S2 Reject ] Customer rejected quote. State: {s2.State.Name}");

        Console.WriteLine("\n>>> SCENARIO S3: Driver Allocation and Reassignment");
        var eligible = dms.ListEligibleDrivers(s1.Ref);
        Console.WriteLine($"[S3 Eligible] Available: [{string.Join(", ", eligible.Select(d => d.Id))}]");
        var alloc = dms.AllocateDriver(s1.Ref);
        Console.WriteLine($"[S3 Allocated] Least-loaded driver {alloc.Id}. State: {s1.State.Name}");
        var reassign = dms.ReassignDriver(s1.Ref, "D-002", "Driver D-001 redirected to urgent pickup");
        Console.WriteLine($"[S3 Reassigned] Reassigned to driver {reassign.Id}.");

        Console.WriteLine("\n>>> SCENARIO S4: Pickup, Simulated Payment, and Proof of Delivery");
        var pay = dms.RecordPickup(s1.Ref, "D-002", true, "cash", "Received exact cash from sender");
        Console.WriteLine($"[S4 Pickup] Completed. State: {s1.State.Name} | Payment: {pay!.Method} ({pay.Status}), Receipt: {pay.ReceiptNo}");
        var proof = dms.RecordDelivery(s1.Ref, "D-002", "Sarah Connor");
        Console.WriteLine($"[S4 Delivery] Delivered! State: {s1.State.Name} | Received by: {proof.ReceivedBy}");

        Console.WriteLine("\n>>> SCENARIO S5: Change of Mind and Cancellations");
        var s5 = dms.SubmitRequest("0400000001", input);
        dms.GenerateQuote(s5.Ref, "standard");
        dms.CancelRequest(s5.Ref, "0400000001", "Sender changed mind before acceptance");
        Console.WriteLine($"[S5 Cancel] {s5.Ref} cancelled. State: {s5.State.Name}");

        Console.WriteLine("\n>>> SCENARIO S6: Track a Delivery");
        var timeline = dms.Track(s1.Ref);
        Console.WriteLine($"[S6 Timeline for {s1.Ref}]:");
        foreach (var r in timeline)
            Console.WriteLine($"  * {r.Timestamp} [{r.StateName}] {r.Actor}: {r.Note}");

        Console.WriteLine(new string('=', 70));
        Console.WriteLine("ALL 6 SCENARIOS EXECUTED SUCCESSFULLY.");
        Console.WriteLine(new string('=', 70));
        try { File.Delete(tmp); } catch { }
    }
}