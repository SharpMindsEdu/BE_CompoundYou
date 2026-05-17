namespace Application.Features.Employees.DTOs;

public record EmployeeDto(
    long Id,
    long UserId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string? Email,
    DateOnly? DateOfBirth,
    DateOnly? HireDate,
    long? TeamId,
    long? ManagerEmployeeId,
    string? ExternalSourceId,
    bool IsActive,
    DateTimeOffset CreatedOn
);
