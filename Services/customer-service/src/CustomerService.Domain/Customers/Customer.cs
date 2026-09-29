using CustomerService.Domain.Common;

namespace CustomerService.Domain.Customers;

public sealed class Customer : AggregateRoot
{
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public Address? Address { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    private Customer()
    {
    }

    private Customer(
        Guid id,
        string firstName,
        string lastName,
        string? email,
        string? phone,
        Address? address,
        string createdBy,
        DateTimeOffset createdAt) : base(id)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Phone = phone;
        Address = address;
        IsActive = true;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public static CustomerMutationResult Create(
        string firstName,
        string lastName,
        string? email,
        string? phone,
        Address? address,
        string createdBy,
        DateTimeOffset createdAt)
    {
        var errors = Validate(firstName, lastName, email, phone);
        if (errors.Count > 0)
        {
            return CustomerMutationResult.Failure(errors);
        }

        var customer = new Customer(
            Guid.NewGuid(),
            firstName.Trim(),
            lastName.Trim(),
            NormalizeOrNull(email),
            NormalizeOrNull(phone),
            address,
            createdBy,
            createdAt);

        return CustomerMutationResult.Success(customer);
    }

    public CustomerMutationResult Update(
        string firstName,
        string lastName,
        string? email,
        string? phone,
        Address? address,
        string modifiedBy,
        DateTimeOffset modifiedAt)
    {
        var errors = Validate(firstName, lastName, email, phone);
        if (errors.Count > 0)
        {
            return CustomerMutationResult.Failure(errors);
        }

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = NormalizeOrNull(email);
        Phone = NormalizeOrNull(phone);
        Address = address;
        ModifiedBy = modifiedBy;
        ModifiedAt = modifiedAt;

        return CustomerMutationResult.Success(this);
    }

    public void Deactivate(string deactivatedBy, DateTimeOffset deactivatedAt)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        ModifiedBy = deactivatedBy;
        ModifiedAt = deactivatedAt;

        AddDomainEvent(new CustomerDeactivated(Id, deactivatedAt, deactivatedBy));
    }

    private static IReadOnlyList<string> Validate(string firstName, string lastName, string? email, string? phone)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(firstName))
        {
            errors.Add("FirstName is required.");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            errors.Add("LastName is required.");
        }

        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
        {
            errors.Add("Either Email or Phone is required.");
        }

        return errors;
    }

    private static string? NormalizeOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
