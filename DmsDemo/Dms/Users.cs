using System.Text.Json.Serialization;

namespace DmsDemo.Dms;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(Customer), "customer")]
[JsonDerivedType(typeof(OfficeStaff), "office")]
[JsonDerivedType(typeof(DeliveryStaff), "driver")]
public abstract class User
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
}

public sealed class Customer : User
{
    public ContactDetails Contact { get; set; } = new("", "");

    [JsonConstructor]
    public Customer(string id, ContactDetails contact)
    {
        Id = id;
        Role = "Customer";
        Contact = contact;
    }

    public string Phone => Id;
    public string Email => Contact.Email;

    public void UpdateContact(string name, string email) =>
        Contact = Contact with { Name = name, Email = email };
}

public sealed class OfficeStaff : User
{
    [JsonConstructor]
    public OfficeStaff(string id, string name)
    {
        Id = id;
        Role = "OfficeStaff";
        Name = name;
    }
}

public sealed class DeliveryStaff : User
{
    public bool Active { get; set; } = true;

    [JsonConstructor]
    public DeliveryStaff(string id, string name, bool active = true)
    {
        Id = id;
        Role = "DeliveryStaff";
        Name = name;
        Active = active;
    }
}