namespace DmsDemo.Dms.Services;

public sealed record AddressInput(string Street, string Suburb, string Postcode);
public sealed record RecipientInput(
    string Name, string Phone, string Email,
    string Street, string Suburb, string Postcode);
public sealed record ParcelInput(
    string Description, double WeightKg,
    double LengthCm, double WidthCm, double HeightCm);
public sealed record RequestInput(
    AddressInput PickupAddress, string PickupDate, string PickupTime,
    RecipientInput Recipient, List<ParcelInput> Parcels);

public sealed class RequestFactory
{
    private readonly ServiceArea _serviceArea;
    private readonly UserDirectory _users;
    private readonly RequestRegistry _registry;

    public RequestFactory(ServiceArea sa, UserDirectory users, RequestRegistry reg)
    {
        _serviceArea = sa; _users = users; _registry = reg;
    }

    public DeliveryRequest CreateRequest(string phone, RequestInput input)
    {
        var customer = _users.GetCustomer(phone);

        var pStreet = Validation.Street(input.PickupAddress.Street, "Pickup street");
        var pSuburb = Validation.Suburb(input.PickupAddress.Suburb, "Pickup suburb");
        var pPostcode = Validation.Postcode(input.PickupAddress.Postcode, "Pickup postcode");
        var pickupAddress = new Address(pStreet, pSuburb, "VIC", pPostcode);
        _serviceArea.ValidateAddress(pickupAddress, "Pickup address");

        Validation.PickupDateTime(input.PickupDate, input.PickupTime);

        var rName = Validation.Name(input.Recipient.Name, "Recipient name");
        var rPhone = Validation.Phone(input.Recipient.Phone, "Recipient phone");
        var rEmail = Validation.Email(input.Recipient.Email, "Recipient email");
        var recipientContact = new ContactDetails(rName, rPhone, rEmail);

        var rStreet = Validation.Street(input.Recipient.Street, "Delivery street");
        var rSuburb = Validation.Suburb(input.Recipient.Suburb, "Delivery suburb");
        var rPostcode = Validation.Postcode(input.Recipient.Postcode, "Delivery postcode");
        var deliveryAddress = new Address(rStreet, rSuburb, "VIC", rPostcode);
        _serviceArea.ValidateAddress(deliveryAddress, "Delivery address");
        Validation.AddressesDiffer(pickupAddress, deliveryAddress);

        var recipient = new Recipient(recipientContact, deliveryAddress);

        if (input.Parcels.Count == 0)
            throw new ValidationException("At least one parcel is required.");
        if (input.Parcels.Count > 5)
            throw new ValidationException("A maximum of 5 parcels is allowed.");

        var specs = input.Parcels.Select(p => Validation.ParcelSpec(
            p.Description, p.WeightKg, p.LengthCm, p.WidthCm, p.HeightCm)).ToList();
        Validation.Parcels(specs);
        var parcels = specs.Select(s => new Parcel(s)).ToList();

        var request = new DeliveryRequest(
            _registry.NextRef(), customer.Phone,
            customer.Contact, pickupAddress,
            input.PickupDate, input.PickupTime,
            recipient, parcels);

        var initial = new StatusRecord(
            "Requested",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            customer.Phone, "Delivery request logged");
        request.History.Add(initial);
        _registry.AddRequest(request);
        foreach (var o in request.GetObservers()) o.OnStatusChange(request, initial);

        return request;
    }
}