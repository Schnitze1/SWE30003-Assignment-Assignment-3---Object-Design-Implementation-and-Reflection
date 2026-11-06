using System.Text.Json;

namespace DmsDemo.Dms.Services;

public sealed class UserDirectory
{
    public Dictionary<string, Customer> Customers { get; private set; } = new();
    public Dictionary<string, User> Staff { get; private set; } = new();
    public List<Availability> Availabilities { get; private set; } = new();

    public Customer? FindCustomer(string phone)
    {
        var key = phone.Replace(" ", "").Trim();
        return Customers.GetValueOrDefault(key);
    }

    public Customer GetCustomer(string phone) =>
        FindCustomer(phone) ?? throw new NotFoundException(
            $"Customer with phone '{phone}' not found.");

    public Customer RegisterCustomer(string name, string phone, string email = "")
    {
        var vName = Validation.Name(name, "Customer name");
        var vPhone = Validation.Phone(phone, "Customer phone");
        var vEmail = Validation.Email(email, "Customer email");
        var contact = new ContactDetails(vName, vPhone, vEmail);
        var customer = new Customer(vPhone, contact);
        Customers[vPhone] = customer;
        return customer;
    }

    public Customer UpdateCustomerContact(string phone, string name, string email = "")
    {
        var customer = GetCustomer(phone);
        var vName = Validation.Name(name, "Customer name");
        var vEmail = Validation.Email(email, "Customer email");
        customer.UpdateContact(vName, vEmail);
        return customer;
    }

    public User? FindStaff(string staffId) =>
        Staff.GetValueOrDefault(staffId.Trim());

    public User GetStaff(string staffId) =>
        FindStaff(staffId) ?? throw new NotFoundException(
            $"Staff member with ID '{staffId}' not found.");

    public void AddStaff(User staff) => Staff[staff.Id] = staff;

    public Availability AddAvailability(string driverId, DateTime start, DateTime end)
    {
        var staff = GetStaff(driverId);
        if (staff is not DeliveryStaff)
            throw new ValidationException($"Staff '{driverId}' is not a delivery driver.");
        if (start >= end)
            throw new ValidationException("Availability start must precede end time.");
        var avail = new Availability(driverId, new TimePeriod(start, end));
        Availabilities.Add(avail);
        return avail;
    }

    public Availability RemoveAvailability(string driverId, int index)
    {
        var driverAvails = Availabilities
            .Select((a, i) => (avail: a, idx: i))
            .Where(x => x.avail.DriverId == driverId)
            .ToList();
        if (index < 0 || index >= driverAvails.Count)
            throw new NotFoundException(
                $"Invalid availability index {index} for driver {driverId}.");
        var (avail, actualIdx) = driverAvails[index];
        Availabilities.RemoveAt(actualIdx);
        return avail;
    }

    public List<Availability> GetDriverAvailabilities(string driverId) =>
        Availabilities.Where(a => a.DriverId == driverId).ToList();

    public List<DeliveryStaff> ListDrivers() =>
        Staff.Values.OfType<DeliveryStaff>().ToList();

    public void SeedDefaults(DateOnly? referenceDate = null)
    {
        Customers.Clear();
        Staff.Clear();
        Availabilities.Clear();

        AddStaff(new OfficeStaff("O-001", "Office Staff 1"));
        AddStaff(new DeliveryStaff("D-001", "Dave Driver"));
        AddStaff(new DeliveryStaff("D-002", "Dan Driver"));
        AddStaff(new DeliveryStaff("D-003", "Diana Driver"));

        RegisterCustomer("Alice Smith", "0400000001", "alice@example.com");

        var baseDate = referenceDate ?? DateOnly.FromDateTime(DateTime.Today);
        for (var i = 0; i < 14; i++)
        {
            var day = baseDate.AddDays(i).ToDateTime(TimeOnly.MinValue);
            var start = day.AddHours(8);
            var end = day.AddHours(18);
            AddAvailability("D-001", start, end);
            AddAvailability("D-002", start, end);
            if (i % 2 == 0) AddAvailability("D-003", start, end);
        }
    }
    public void Restore(
        Dictionary<string, Customer> customers,
        Dictionary<string, User> staff,
        List<Availability> availabilities)
    {
        Customers = customers;
        Staff = staff;
        Availabilities = availabilities;
    }
}