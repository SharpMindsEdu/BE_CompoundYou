namespace Application.Features.Departments.DTOs;

public record DepartmentDto(long Id, string Name, long? ParentDepartmentId, DateTimeOffset CreatedOn);
