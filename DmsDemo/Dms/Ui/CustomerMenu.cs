using DmsDemo.Dms.Services;

namespace DmsDemo.Dms.Ui;

public sealed class CustomerMenu : UiBase
{
    public CustomerMenu(DeliveryManagementSystem dms) : base(dms) { }

    public void Show()
    {
        Console.WriteLine("\nCustomer (identify by phone; register if new)");
        var phone = Prompt("Enter phone number: ");
        if (string.IsNullOrEmpty(phone)) return;
        var cleanPhone = phone.Replace(" ", "").Trim();
        var customer = Dms.IdentifyCustomer(cleanPhone);

        if (customer is null)
        {
            Console.WriteLine($"No customer account found for phone: {phone}");
            if (Prompt("Register as a new customer? (y/n): ").ToLowerInvariant() != "y")
                return;
            var name = Prompt("Enter full name: ");
            var email = Prompt("Enter email (optional): ");
            try
            {
                customer = Dms.RegisterCustomer(name, cleanPhone, email);
                Success($"Registered customer '{customer.Contact.Name}' ({customer.Phone}).");
            }
            catch (ValidationException e) { Error(e.Message); return; }
        }

        while (Running)
        {
            Console.WriteLine("\nCustomer (identify by phone; register if new)");
            Console.WriteLine("  1 New delivery request (form, then REVIEW: edit a field / discard / confirm)");
            Console.WriteLine("  2 My requests    3 View quote, accept or reject");
            Console.WriteLine("  4 Cancel a request    5 Update my contact details    6 Inbox    0 Back");
            var choice = Prompt("Choice: ");
            switch (choice)
            {
                case "1": NewRequest(customer.Phone); break;
                case "2": ListRequests(customer.Phone); break;
                case "3": ViewQuote(customer.Phone); break;
                case "4": CancelRequest(customer.Phone); break;
                case "5": UpdateContact(customer.Phone); break;
                case "6": ShowInbox(customer.Phone); break;
                case "0": return;
                default: Error("Invalid selection."); break;
            }
        }
    }

    private void NewRequest(string phone)
    {
        Console.WriteLine("\n--- New Delivery Request Form ---");
        var pStreet = PromptValidated("Pickup Street: ", v => Validation.Street(v, "Pickup street"));
        var pSuburb = PromptValidated("Pickup Suburb: ", v => Validation.Suburb(v, "Pickup suburb"));
        var pPostcode = PromptValidated("Pickup Postcode (3000-3207): ",
            v => Validation.Postcode(v, "Pickup postcode"));

        DateOnly pDate; TimeOnly pTime;
        while (true)
        {
            var ds = Prompt("Pickup Date (YYYY-MM-DD): ");
            var ts = Prompt("Pickup Time (HH:MM, 08:00-18:00): ");
            try { (pDate, pTime) = Validation.PickupDateTime(ds, ts); break; }
            catch (ValidationException e) { Error(e.Message); }
        }

        var rName = PromptValidated("Recipient Name: ", v => Validation.Name(v, "Recipient name"));
        var rPhone = PromptValidated("Recipient Phone: ", v => Validation.Phone(v, "Recipient phone"));
        var rEmail = PromptValidated("Recipient Email (optional): ", v => Validation.Email(v, "Recipient email"));

        string rStreet, rSuburb, rPostcode;
        while (true)
        {
            rStreet = PromptValidated("Delivery Street: ", v => Validation.Street(v, "Delivery street"));
            rSuburb = PromptValidated("Delivery Suburb: ", v => Validation.Suburb(v, "Delivery suburb"));
            rPostcode = PromptValidated("Delivery Postcode (3000-3207): ",
                v => Validation.Postcode(v, "Delivery postcode"));
            if (pStreet.ToLowerInvariant() == rStreet.ToLowerInvariant()
                && pSuburb.ToLowerInvariant() == rSuburb.ToLowerInvariant()
                && pPostcode == rPostcode)
            {
                Error("Pickup and delivery addresses must differ.");
                continue;
            }
            break;
        }

        int pCount;
        while (true)
        {
            if (int.TryParse(Prompt("Number of parcels (1 to 5): "), out pCount)
                && pCount >= 1 && pCount <= 5) break;
            Error("Must be between 1 and 5 parcels.");
        }

        var parcels = new List<ParcelInput>();
        for (var i = 1; i <= pCount; i++)
        {
            Console.WriteLine($"\nParcel {i} of {pCount}:");
            parcels.Add(PromptParcel());
        }

        var input = new RequestInput(
            new AddressInput(pStreet, pSuburb, pPostcode),
            pDate.ToString("yyyy-MM-dd"), pTime.ToString("HH:mm"),
            new RecipientInput(rName, rPhone, rEmail, rStreet, rSuburb, rPostcode),
            parcels);

        while (true)
        {
            Console.WriteLine("\nREVIEW: edit a field / discard / confirm");
            Console.WriteLine($"1. Pickup Address : {pStreet}, {pSuburb} VIC {pPostcode}");
            Console.WriteLine($"2. Pickup Schedule: {input.PickupDate} at {input.PickupTime}");
            Console.WriteLine($"3. Recipient Info : {rName} ({rPhone}) {rEmail}");
            Console.WriteLine($"4. Delivery Addr  : {rStreet}, {rSuburb} VIC {rPostcode}");
            Console.WriteLine($"5. Parcels ({parcels.Count} items):");
            for (var idx = 0; idx < parcels.Count; idx++)
            {
                var p = parcels[idx];
                Console.WriteLine($"   [{idx + 1}] {p.Description} - {p.WeightKg}kg " +
                    $"({p.LengthCm}x{p.WidthCm}x{p.HeightCm}cm)");
            }
            Console.WriteLine(new string('-', 60));
            Console.WriteLine("Options: 1 Confirm   2 Edit a field   3 Discard");
            var rev = Prompt("Select option: ");
            if (rev == "1")
            {
                try
                {
                    var req = Dms.SubmitRequest(phone, input);
                    Success($"Request successfully logged! Tracking Reference: {req.Ref}");
                    return;
                }
                catch (ValidationException e) { Error(e.Message); }
            }
            else if (rev == "2") EditField(input, ref pStreet, ref pSuburb, ref pPostcode,
                ref rName, ref rPhone, ref rEmail, ref rStreet, ref rSuburb, ref rPostcode,
                parcels, ref input);
            else if (rev == "3") { Success("Request discarded. No request was created."); return; }
            else Error("Invalid selection. Choose 1, 2, or 3.");
        }
    }

    private void EditField(RequestInput input,
        ref string pStreet, ref string pSuburb, ref string pPostcode,
        ref string rName, ref string rPhone, ref string rEmail,
        ref string rStreet, ref string rSuburb, ref string rPostcode,
        List<ParcelInput> parcels, ref RequestInput current)
    {
        Console.WriteLine("\nWhich field would you like to edit?");
        Console.WriteLine("1 Pickup Address\n2 Pickup Schedule\n3 Recipient Contact\n4 Delivery Address\n5 Parcels\n0 Cancel");
        var choice = Prompt("Choice: ");
        switch (choice)
        {
            case "1":
                pStreet = PromptValidated("New Pickup Street: ", v => Validation.Street(v, "Street"));
                pSuburb = PromptValidated("New Pickup Suburb: ", v => Validation.Suburb(v, "Suburb"));
                pPostcode = PromptValidated("New Pickup Postcode: ", v => Validation.Postcode(v, "Postcode"));
                current = current with { PickupAddress = new AddressInput(pStreet, pSuburb, pPostcode) };
                break;
            case "2":
                while (true)
                {
                    var ds = Prompt("New Pickup Date (YYYY-MM-DD): ");
                    var ts = Prompt("New Pickup Time (HH:MM): ");
                    try
                    {
                        var (d, t) = Validation.PickupDateTime(ds, ts);
                        current = current with
                        {
                            PickupDate = d.ToString("yyyy-MM-dd"),
                            PickupTime = t.ToString("HH:mm"),
                        };
                        break;
                    }
                    catch (ValidationException e) { Error(e.Message); }
                }
                break;
            case "3":
                rName = PromptValidated("New Recipient Name: ", v => Validation.Name(v, "Name"));
                rPhone = PromptValidated("New Recipient Phone: ", v => Validation.Phone(v, "Phone"));
                rEmail = PromptValidated("New Recipient Email (optional): ", v => Validation.Email(v, "Email"));
                current = current with
                {
                    Recipient = current.Recipient with { Name = rName, Phone = rPhone, Email = rEmail }
                };
                break;
            case "4":
                rStreet = PromptValidated("New Delivery Street: ", v => Validation.Street(v, "Street"));
                rSuburb = PromptValidated("New Delivery Suburb: ", v => Validation.Suburb(v, "Suburb"));
                rPostcode = PromptValidated("New Delivery Postcode: ", v => Validation.Postcode(v, "Postcode"));
                current = current with
                {
                    Recipient = current.Recipient with
                    { Street = rStreet, Suburb = rSuburb, Postcode = rPostcode }
                };
                break;
            case "5":
                parcels.Clear();
                int count;
                while (true)
                {
                    if (int.TryParse(Prompt("New number of parcels (1 to 5): "), out count)
                        && count >= 1 && count <= 5) break;
                    Error("Must be 1 to 5.");
                }
                for (var i = 1; i <= count; i++) parcels.Add(PromptParcel());
                current = current with { Parcels = parcels };
                break;
        }
    }

    private ParcelInput PromptParcel()
    {
        while (true)
        {
            var desc = Prompt("  Description (e.g. Box of books): ");
            var w = Prompt("  Weight in kg (0.1 - 30.0): ");
            var l = Prompt("  Length in cm (1 - 150): ");
            var wd = Prompt("  Width in cm (1 - 150): ");
            var h = Prompt("  Height in cm (1 - 150): ");
            try
            {
                var spec = Validation.ParcelSpec(desc,
                    double.Parse(w), double.Parse(l), double.Parse(wd), double.Parse(h));
                return new ParcelInput(spec.Description, spec.WeightKg,
                    spec.LengthCm, spec.WidthCm, spec.HeightCm);
            }
            catch (FormatException) { Error("Please enter numeric values."); }
            catch (ValidationException e) { Error(e.Message); }
        }
    }

    private void ListRequests(string phone)
    {
        var reqs = Dms.ListRequests(phone: phone);
        if (reqs.Count == 0) { Console.WriteLine("\nYou have no requests.\n"); return; }
        Console.WriteLine($"\nMy Delivery Requests ({reqs.Count} total):");
        Console.WriteLine(new string('-', 68));
        Console.WriteLine($"{"Reference",-12} {"Status",-14} {"Pickup Date",-12} {"Parcels",-8} Quote");
        Console.WriteLine(new string('-', 68));
        foreach (var r in reqs)
        {
            var q = r.CurrentQuote();
            var qs = q is null ? "No quote" : $"${q.Amount:F2} ({q.Status})";
            Console.WriteLine($"{r.Ref,-12} {r.State.Name,-14} {r.PickupDate,-12} {r.Parcels.Count,-8} {qs}");
        }
        Console.WriteLine(new string('-', 68));
    }

    private void ViewQuote(string phone)
    {
        var quoted = Dms.ListRequests(phone: phone)
            .Where(r => r.State.Name == "Quoted").ToList();
        if (quoted.Count == 0) { Console.WriteLine("\nNo requests awaiting your quote decision.\n"); return; }
        foreach (var r in quoted)
        {
            var q = r.CurrentQuote();
            Console.WriteLine($"  {r.Ref}: {q?.Strategy} Quote of ${q?.Amount:F2}");
        }
        var @ref = Prompt("\nEnter reference to review quote: ").ToUpperInvariant();
        try
        {
            var req = Dms.GetRequest(@ref);
            if (req.CustomerPhone != phone)
            { Error("This request does not belong to your account."); return; }
            if (req.State.Name != "Quoted")
            { Error($"Request {@ref} is in '{req.State.Name}', not Quoted."); return; }
            var q = req.CurrentQuote();
            if (q is null) { Error("No quote found."); return; }
            Console.WriteLine($"\nQuote for {@ref}:");
            Console.WriteLine($"  Strategy : {q.Strategy}");
            Console.WriteLine($"  Amount   : ${q.Amount:F2}");
            if (!string.IsNullOrEmpty(q.OverrideReason))
                Console.WriteLine($"  Override : {q.OverrideReason}");
            Console.WriteLine("\n1 Accept quote   2 Reject quote   0 Back");
            var decision = Prompt("Decision: ");
            if (decision == "1")
            {
                Dms.RespondToQuote(@ref, true);
                Success($"Quote accepted for {@ref}! Office will allocate driver.");
            }
            else if (decision == "2")
            {
                Dms.RespondToQuote(@ref, false);
                Success($"Quote rejected for {@ref}. Request closed as Rejected.");
            }
        }
        catch (DmsException e) { Error(e.Message); }
    }

    private void CancelRequest(string phone)
    {
        var @ref = Prompt("Enter reference to cancel: ").ToUpperInvariant();
        if (string.IsNullOrEmpty(@ref)) return;
        try
        {
            var req = Dms.GetRequest(@ref);
            if (req.CustomerPhone != phone)
            { Error("This request does not belong to your account."); return; }
            var reason = Prompt("Reason for cancellation: ");
            Dms.CancelRequest(@ref, phone, reason);
            Success($"Request {@ref} has been cancelled.");
        }
        catch (DmsException e) { Error(e.Message); }
    }

    private void UpdateContact(string phone)
    {
        var name = Prompt("Enter new full name: ");
        var email = Prompt("Enter new email (optional): ");
        try
        {
            var c = Dms.UpdateCustomerContact(phone, name, email);
            Success($"Contact details updated: {c.Contact.Name} | {c.Contact.Email}");
        }
        catch (ValidationException e) { Error(e.Message); }
    }
}