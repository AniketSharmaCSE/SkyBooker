using NUnit.Framework;
using PassengerService.DTOs;
using PassengerService.Services;

namespace AirlineBooking.Tests;

[TestFixture]
public class PassengerServiceTests
{
    [Test]
    public async Task UpsertProfileAsync_RejectsMissingPhoneNumber()
    {
        await using var db = TestHelpers.CreatePassengerDbContext();
        var service = new PassengerManagementService(db);

        var result = await service.UpsertProfileAsync(new UpsertProfileRequest
        {
            DateOfBirth = "1995-06-15",
            PassportNumber = "A1234567",
            Nationality = "Indian"
        }, 1, "Test Passenger", "passenger@example.com");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Phone number is required."));
    }

    [Test]
    public async Task UpsertProfileAsync_RejectsFutureDateOfBirth()
    {
        await using var db = TestHelpers.CreatePassengerDbContext();
        var service = new PassengerManagementService(db);

        var result = await service.UpsertProfileAsync(new UpsertProfileRequest
        {
            PhoneNumber = "9876543210",
            DateOfBirth = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd"),
            PassportNumber = "A1234567",
            Nationality = "Indian"
        }, 1, "Test Passenger", "passenger@example.com");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Date of birth cannot be in the future."));
    }

    [Test]
    public async Task UpsertProfileAsync_RejectsPassportUsedByAnotherUser()
    {
        await using var db = TestHelpers.CreatePassengerDbContext();
        var service = new PassengerManagementService(db);
        var request = new UpsertProfileRequest
        {
            PhoneNumber = "9876543210",
            DateOfBirth = "1995-06-15",
            PassportNumber = "A1234567",
            Nationality = "Indian"
        };

        var first = await service.UpsertProfileAsync(request, 1, "First Passenger", "first@example.com");
        var second = await service.UpsertProfileAsync(request, 2, "Second Passenger", "second@example.com");

        Assert.That(first.Success, Is.True);
        Assert.That(second.Success, Is.False);
        Assert.That(second.Message, Is.EqualTo("This passport number is already registered to another account."));
    }

    [Test]
    public async Task UpsertProfileAsync_RejectsMissingPassportNumber()
    {
        await using var db = TestHelpers.CreatePassengerDbContext();
        var service = new PassengerManagementService(db);

        var result = await service.UpsertProfileAsync(new UpsertProfileRequest
        {
            PhoneNumber = "9876543210",
            DateOfBirth = "1995-06-15",
            Nationality = "Indian"
        }, 1, "Test Passenger", "passenger@example.com");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Passport number is required."));
    }

    [Test]
    public async Task UpsertProfileAsync_RejectsMissingNationality()
    {
        await using var db = TestHelpers.CreatePassengerDbContext();
        var service = new PassengerManagementService(db);

        var result = await service.UpsertProfileAsync(new UpsertProfileRequest
        {
            PhoneNumber = "9876543210",
            DateOfBirth = "1995-06-15",
            PassportNumber = "A1234567"
        }, 1, "Test Passenger", "passenger@example.com");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Nationality is required."));
    }

    [Test]
    public async Task UpsertProfileAsync_RejectsInvalidDateOfBirth()
    {
        await using var db = TestHelpers.CreatePassengerDbContext();
        var service = new PassengerManagementService(db);

        var result = await service.UpsertProfileAsync(new UpsertProfileRequest
        {
            PhoneNumber = "9876543210",
            DateOfBirth = "not-a-date",
            PassportNumber = "A1234567",
            Nationality = "Indian"
        }, 1, "Test Passenger", "passenger@example.com");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.StartWith("Date of birth must be in yyyy-MM-dd format"));
    }

    [Test]
    public async Task UpsertProfileAsync_CreatesProfileWithTrimmedValues()
    {
        await using var db = TestHelpers.CreatePassengerDbContext();
        var service = new PassengerManagementService(db);

        var result = await service.UpsertProfileAsync(new UpsertProfileRequest
        {
            PhoneNumber = " 9876543210 ",
            DateOfBirth = "1995-06-15",
            PassportNumber = " a1234567 ",
            Nationality = " Indian "
        }, 1, "Test Passenger", "passenger@example.com");

        Assert.That(result.Success, Is.True);
        Assert.That(result.Profile!.PhoneNumber, Is.EqualTo("9876543210"));
        Assert.That(result.Profile.PassportNumber, Is.EqualTo("A1234567"));
        Assert.That(result.Profile.Nationality, Is.EqualTo("Indian"));
    }

    [Test]
    public async Task UpsertProfileAsync_UpdatesExistingProfile()
    {
        await using var db = TestHelpers.CreatePassengerDbContext();
        var service = new PassengerManagementService(db);

        await service.UpsertProfileAsync(new UpsertProfileRequest
        {
            PhoneNumber = "9876543210",
            DateOfBirth = "1995-06-15",
            PassportNumber = "A1234567",
            Nationality = "Indian"
        }, 1, "Test Passenger", "passenger@example.com");

        var result = await service.UpsertProfileAsync(new UpsertProfileRequest
        {
            PhoneNumber = "9999999999",
            DateOfBirth = "1996-07-16",
            PassportNumber = "B7654321",
            Nationality = "Indian"
        }, 1, "Test Passenger", "passenger@example.com");

        Assert.That(result.Success, Is.True);
        Assert.That(result.Profile!.PhoneNumber, Is.EqualTo("9999999999"));
        Assert.That(result.Profile.PassportNumber, Is.EqualTo("B7654321"));
    }

    [Test]
    public async Task GetMyProfileAsync_ReturnsNotFoundWhenProfileMissing()
    {
        await using var db = TestHelpers.CreatePassengerDbContext();
        var service = new PassengerManagementService(db);

        var result = await service.GetMyProfileAsync(1);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("No profile found"));
    }

    [Test]
    public async Task GetByUserIdAsync_ReturnsExistingPassenger()
    {
        await using var db = TestHelpers.CreatePassengerDbContext();
        var service = new PassengerManagementService(db);
        await service.UpsertProfileAsync(new UpsertProfileRequest
        {
            PhoneNumber = "9876543210",
            DateOfBirth = "1995-06-15",
            PassportNumber = "A1234567",
            Nationality = "Indian"
        }, 1, "Test Passenger", "passenger@example.com");

        var result = await service.GetByUserIdAsync(1);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Profile!.Email, Is.EqualTo("passenger@example.com"));
    }

    [Test]
    public async Task GetAllPassengersAsync_ReturnsPassengersSortedByName()
    {
        await using var db = TestHelpers.CreatePassengerDbContext();
        var service = new PassengerManagementService(db);
        await service.UpsertProfileAsync(ValidRequest("A1234567"), 1, "Zara Passenger", "zara@example.com");
        await service.UpsertProfileAsync(ValidRequest("B7654321"), 2, "Aman Passenger", "aman@example.com");

        var result = await service.GetAllPassengersAsync();

        Assert.That(result.TotalCount, Is.EqualTo(2));
        Assert.That(result.Passengers.Select(p => p.FullName), Is.EqualTo(new[] { "Aman Passenger", "Zara Passenger" }));
    }

    [Test]
    public async Task PassengerExistsAsync_ReturnsTrueOnlyForExistingPassenger()
    {
        await using var db = TestHelpers.CreatePassengerDbContext();
        var service = new PassengerManagementService(db);
        await service.UpsertProfileAsync(ValidRequest("A1234567"), 1, "Test Passenger", "test@example.com");

        Assert.That(await service.PassengerExistsAsync(1), Is.True);
        Assert.That(await service.PassengerExistsAsync(2), Is.False);
    }

    private static UpsertProfileRequest ValidRequest(string passportNumber) => new()
    {
        PhoneNumber = "9876543210",
        DateOfBirth = "1995-06-15",
        PassportNumber = passportNumber,
        Nationality = "Indian"
    };
}
