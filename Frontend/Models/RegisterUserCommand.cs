namespace Frontend.Models;

public record RegisterUserCommand(string DisplayName, string? Email, string? PhoneNumber);
