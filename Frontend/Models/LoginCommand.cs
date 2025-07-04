namespace Frontend.Models;

public record LoginCommand(string Code, string? Email, string? PhoneNumber);
