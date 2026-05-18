using Application.Features.Career.DTOs;

namespace Application.Features.CareerPaths.Services;

public interface ICareerReadinessService
{
    Task<CareerPathDto> CalculateAsync(
        long employeeId,
        long? targetRoleProfileId = null,
        CancellationToken ct = default
    );
}
