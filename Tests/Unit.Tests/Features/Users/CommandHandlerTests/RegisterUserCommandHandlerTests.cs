using Application.Common;
using Application.Features.Users.Commands;
using FluentValidation;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Users.CommandHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.UserTests)]
public class RegisterUserCommandHandlerTests(PostgreSqlRepositoryTestDatabaseFixture fixture, ITestOutputHelper outputHelper)
    : FeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task RegisterUser_WithValidData_ShouldCreateUserAndReturnToken()
    {
        // Arrange
        
        var displayName = "Test User";
        var email = "test@example.com";
        var command = new RegisterUser.RegisterUserCommand(displayName, email, null);

        // Act
        var result = await Send(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.False(string.IsNullOrEmpty(result.Data.Token));

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.Data.Token);
        var userIdClaim = jwt.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier);

        Assert.NotNull(userIdClaim);
        Assert.True(long.TryParse(userIdClaim!.Value, out _));
    }
    
    [Fact]
    public async Task RegisterUser_WithExistingEmail_ShouldFailWithConflict()
    {
        // Arrange
        var email = "duplicate@example.com";
        var existingUser = new Domain.Entities.User { DisplayName = "Existing", Email = email };
        PersistWithDatabase(db => db.Add(existingUser));

        var command = new RegisterUser.RegisterUserCommand("New User", email, null);

        // Act
        var result = await Send(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Contains(ErrorResults.EmailInUse, result.ErrorMessage);
    }
    
    [Fact]
    public async Task RegisterUser_WithExistingPhoneNumber_ShouldFailWithConflict()
    {
        // Arrange
        var phone = "+49123456789";
        var existingUser = new Domain.Entities.User { DisplayName = "Existing", PhoneNumber = phone };
        PersistWithDatabase(db => db.Add(existingUser));

        var command = new RegisterUser.RegisterUserCommand("New User", null, phone);

        // Act
        var result = await Send(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Contains(ErrorResults.PhoneInUse, result.ErrorMessage);
    }
    
    [Fact]
    public async Task RegisterUser_WithoutEmailAndPhone_ShouldFailValidation()
    {
        // Arrange
        var command = new RegisterUser.RegisterUserCommand("New User", null, null);

        // Act
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken));
        // Assert
        Assert.Contains(ValidationErrors.EmailAndPhoneNumberMissing, ex.Message);

    }
    
    [Fact]
    public async Task RegisterUser_WithPhoneOnly_ShouldSucceed()
    {
        // Arrange
        var phone = "+49111111111";
        var command = new RegisterUser.RegisterUserCommand("Phone User", null, phone);

        // Act
        var result = await Send(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.False(string.IsNullOrEmpty(result.Data.Token));

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.Data.Token);
        var userIdClaim = jwt.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier);

        Assert.NotNull(userIdClaim);
        Assert.True(long.TryParse(userIdClaim!.Value, out _));
    }




}