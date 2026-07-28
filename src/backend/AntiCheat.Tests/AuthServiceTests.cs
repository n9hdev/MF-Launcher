using System.Security.Claims;
using AntiCheat.Core.Configuration;
using AntiCheat.Core.Data;
using AntiCheat.Core.Data.Entities;
using AntiCheat.Core.Interfaces;
using AntiCheat.Core.Services;
using AntiCheat.Shared.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AntiCheat.Tests;

public class AuthServiceTests
{
    private readonly AuthService _sut;
    private readonly AppDbContext _db;
    private readonly Mock<ILogger<AuthService>> _loggerMock = new();
    private readonly Mock<IHardwareIdProvider> _hwidMock = new();

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AuthTestDb_{Guid.NewGuid()}")
            .Options;
        _db = new AppDbContext(options);

        var jwtSettings = Options.Create(new JwtSettings
        {
            Secret = "test-secret-key-that-is-at-least-32-characters!",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7,
        });

        _hwidMock.Setup(x => x.GetHardwareId()).Returns("test-hwid-mock");

        _sut = new AuthService(_loggerMock.Object, jwtSettings, _db, _hwidMock.Object);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        var password = "test-password";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        _db.Users.Add(new UserEntity
        {
            Id = "1",
            Username = "testuser",
            PasswordHash = hash,
            DisplayName = "Test User",
            Role = "player",
        });
        await _db.SaveChangesAsync();

        var request = new LoginRequest { Username = "testuser", Password = password };
        var result = await _sut.LoginAsync(request);

        result.AccessToken.Should().NotBeNullOrEmpty();
        result.User.Should().NotBeNull();
        result.User.Username.Should().Be("testuser");
        result.User.Role.Should().Be("player");
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsUnauthorized()
    {
        _db.Users.Add(new UserEntity
        {
            Id = "2",
            Username = "user2",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct"),
            DisplayName = "User 2",
            Role = "moderator",
        });
        await _db.SaveChangesAsync();

        var request = new LoginRequest { Username = "user2", Password = "wrong" };

        await FluentActions.Awaiting(() => _sut.LoginAsync(request))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task LoginAsync_NonExistentUser_ThrowsUnauthorized()
    {
        var request = new LoginRequest { Username = "nobody", Password = "pwd" };

        await FluentActions.Awaiting(() => _sut.LoginAsync(request))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RegisterAsync_CreatesNewUser()
    {
        var request = new RegisterRequest
        {
            Username = "newuser",
            Password = "Str0ng!Pass",
            DisplayName = "New User",
        };

        var result = await _sut.RegisterAsync(request);

        result.AccessToken.Should().NotBeNullOrEmpty();
        result.User.Username.Should().Be("newuser");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == "newuser");
        user.Should().NotBeNull();
        user!.PasswordHash.Should().NotBeNullOrEmpty();
        BCrypt.Net.BCrypt.Verify("Str0ng!Pass", user.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task RegisterAsync_DuplicateUsername_ThrowsInvalidOperation()
    {
        _db.Users.Add(new UserEntity
        {
            Id = "3",
            Username = "existing",
            PasswordHash = "hash",
            DisplayName = "Existing",
            Role = "player",
        });
        await _db.SaveChangesAsync();

        var request = new RegisterRequest
        {
            Username = "existing",
            Password = "Str0ng!Pass",
            DisplayName = "Duplicate",
        };

        await FluentActions.Awaiting(() => _sut.RegisterAsync(request))
            .Should().ThrowAsync<InvalidOperationException>();
    }
}
