using AuthService.DTOs;
using AuthService.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace AirlineBooking.Tests;

[TestFixture]
public class AuthServiceTests
{
    [Test]
    public async Task RegisterAsync_CreatesUserWithNormalizedEmailAndHashedPassword()
    {
        await using var db = TestHelpers.CreateAuthDbContext();
        var service = new AuthService.Services.AuthService(db, TestHelpers.CreateConfiguration());

        var result = await service.RegisterAsync(new RegisterRequest
        {
            FullName = "Test Passenger",
            Email = "Passenger@Example.COM",
            Password = "secret123",
            Role = "PASSENGER"
        });

        var user = await db.Users.SingleAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(user.Email, Is.EqualTo("passenger@example.com"));
        Assert.That(user.PasswordHash, Is.Not.EqualTo("secret123"));
        Assert.That(BCrypt.Net.BCrypt.Verify("secret123", user.PasswordHash), Is.True);
    }

    [Test]
    public async Task RegisterAsync_RejectsDuplicateEmail()
    {
        await using var db = TestHelpers.CreateAuthDbContext();
        var service = new AuthService.Services.AuthService(db, TestHelpers.CreateConfiguration());
        var request = new RegisterRequest
        {
            FullName = "Test Passenger",
            Email = "passenger@example.com",
            Password = "secret123",
            Role = "PASSENGER"
        };

        await service.RegisterAsync(request);
        var duplicate = await service.RegisterAsync(request);

        Assert.That(duplicate.Success, Is.False);
        Assert.That(duplicate.Message, Is.EqualTo("Email already registered."));
    }

    [Test]
    public async Task LoginAsync_ReturnsTokenForValidCredentials()
    {
        await using var db = TestHelpers.CreateAuthDbContext();
        var service = new AuthService.Services.AuthService(db, TestHelpers.CreateConfiguration());

        await service.RegisterAsync(new RegisterRequest
        {
            FullName = "Test Passenger",
            Email = "passenger@example.com",
            Password = "secret123",
            Role = "PASSENGER"
        });

        var login = await service.LoginAsync(new LoginRequest
        {
            Email = "passenger@example.com",
            Password = "secret123"
        });

        Assert.That(login.Success, Is.True);
        Assert.That(login.Response, Is.Not.Null);
        Assert.That(login.Response!.Token, Is.Not.Empty);
    }

    [Test]
    public async Task RegisterAsync_RejectsInvalidRole()
    {
        await using var db = TestHelpers.CreateAuthDbContext();
        var service = new AuthService.Services.AuthService(db, TestHelpers.CreateConfiguration());

        var result = await service.RegisterAsync(new RegisterRequest
        {
            FullName = "Test User",
            Email = "test@example.com",
            Password = "secret123",
            Role = "ADMIN"
        });

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Role must be PASSENGER or STAFF."));
    }

    [Test]
    public async Task RegisterAsync_NormalizesStaffRole()
    {
        await using var db = TestHelpers.CreateAuthDbContext();
        var service = new AuthService.Services.AuthService(db, TestHelpers.CreateConfiguration());

        var result = await service.RegisterAsync(new RegisterRequest
        {
            FullName = "Staff User",
            Email = "staff@example.com",
            Password = "secret123",
            Role = "staff"
        });

        var user = await db.Users.SingleAsync();
        Assert.That(result.Success, Is.True);
        Assert.That(user.Role, Is.EqualTo("STAFF"));
    }

    [Test]
    public async Task LoginAsync_RejectsUnknownEmail()
    {
        await using var db = TestHelpers.CreateAuthDbContext();
        var service = new AuthService.Services.AuthService(db, TestHelpers.CreateConfiguration());

        var result = await service.LoginAsync(new LoginRequest
        {
            Email = "missing@example.com",
            Password = "secret123"
        });

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Invalid email or password."));
    }

    [Test]
    public async Task LoginAsync_RejectsWrongPassword()
    {
        await using var db = TestHelpers.CreateAuthDbContext();
        var service = new AuthService.Services.AuthService(db, TestHelpers.CreateConfiguration());
        await service.RegisterAsync(new RegisterRequest
        {
            FullName = "Test Passenger",
            Email = "passenger@example.com",
            Password = "secret123"
        });

        var result = await service.LoginAsync(new LoginRequest
        {
            Email = "passenger@example.com",
            Password = "wrong-password"
        });

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Invalid email or password."));
    }

    [Test]
    public async Task LoginAsync_TrimsAndNormalizesEmail()
    {
        await using var db = TestHelpers.CreateAuthDbContext();
        var service = new AuthService.Services.AuthService(db, TestHelpers.CreateConfiguration());
        await service.RegisterAsync(new RegisterRequest
        {
            FullName = "Test Passenger",
            Email = "passenger@example.com",
            Password = "secret123"
        });

        var result = await service.LoginAsync(new LoginRequest
        {
            Email = " PASSENGER@EXAMPLE.COM ",
            Password = "secret123"
        });

        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task Register_ReturnsBadRequestWhenRequiredFieldsAreMissing()
    {
        await using var db = TestHelpers.CreateAuthDbContext();
        var controller = new AuthController(
            new AuthService.Services.AuthService(db, TestHelpers.CreateConfiguration()));

        var response = await controller.Register(new RegisterRequest
        {
            Email = "",
            Password = "secret123",
            FullName = "Test User"
        });

        Assert.That(response, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Register_ReturnsBadRequestForShortPassword()
    {
        await using var db = TestHelpers.CreateAuthDbContext();
        var controller = new AuthController(
            new AuthService.Services.AuthService(db, TestHelpers.CreateConfiguration()));

        var response = await controller.Register(new RegisterRequest
        {
            Email = "test@example.com",
            Password = "12345",
            FullName = "Test User"
        });

        Assert.That(response, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Login_ReturnsBadRequestWhenCredentialsAreMissing()
    {
        await using var db = TestHelpers.CreateAuthDbContext();
        var controller = new AuthController(
            new AuthService.Services.AuthService(db, TestHelpers.CreateConfiguration()));

        var response = await controller.Login(new LoginRequest
        {
            Email = "",
            Password = ""
        });

        Assert.That(response, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Login_ReturnsUnauthorizedForInvalidCredentials()
    {
        await using var db = TestHelpers.CreateAuthDbContext();
        var controller = new AuthController(
            new AuthService.Services.AuthService(db, TestHelpers.CreateConfiguration()));

        var response = await controller.Login(new LoginRequest
        {
            Email = "missing@example.com",
            Password = "secret123"
        });

        Assert.That(response, Is.TypeOf<UnauthorizedObjectResult>());
    }
}
