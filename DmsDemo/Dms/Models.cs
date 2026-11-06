namespace DmsDemo.Dms;

public record Address(string Street, string Suburb, string State, string Postcode);

public record ContactDetails(string Name, string Phone, string Email = "");

public record Recipient(ContactDetails Contact, Address Address);

public record ParcelSpecification(
    string Description, double WeightKg,
    double LengthCm, double WidthCm, double HeightCm);

public record TimePeriod(DateTime Start, DateTime End)
{
    public bool Contains(DateTime dt) => Start <= dt && dt <= End;
}

public record Availability(string DriverId, TimePeriod Period);

public record StatusRecord(
    string StateName, string Timestamp, string Actor, string Note = "");

public record ProofOfDelivery(string ReceivedBy, string Timestamp, string Note)
{
    public static ProofOfDelivery Create(string receivedBy, string note = "") =>
        new(receivedBy, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), note);
}

public record Payment(
    string RequestRef, double Amount, string Method,
    string Status, string ReceiptNo, string Timestamp);