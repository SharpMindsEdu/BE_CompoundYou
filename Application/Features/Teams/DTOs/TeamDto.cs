namespace Application.Features.Teams.DTOs;

public record TeamDto(
    long Id,
    long DepartmentId,
    string Name,
    long? ManagerEmployeeId,
    DateTimeOffset CreatedOn
);
