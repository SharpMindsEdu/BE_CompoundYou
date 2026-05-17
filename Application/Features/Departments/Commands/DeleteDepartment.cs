using Application.Authorization;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Departments.Commands;

public static class DeleteDepartment
{
    public const string Endpoint = "api/departments/{id:long}";

    public record DeleteDepartmentCommand(long Id) : ICommandRequest<Result<bool>>, IAuditable
    {
        public string AuditAction => "department.delete";
        public string AuditEntityType => nameof(Department);
        public long? AuditEntityId => Id;
    }

    internal sealed class Handler(IRepository<Department> repo)
        : IRequestHandler<DeleteDepartmentCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(DeleteDepartmentCommand request, CancellationToken ct)
        {
            var department = await repo.GetById(request.Id);
            if (department is null)
                return Result<bool>.Failure(TenancyErrors.DepartmentNotFound, ResultStatus.NotFound);

            repo.Remove(department);
            return Result<bool>.Success(true);
        }
    }
}

public class DeleteDepartmentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                DeleteDepartment.Endpoint,
                async (long id, ISender sender) =>
                {
                    var result = await sender.Send(new DeleteDepartment.DeleteDepartmentCommand(id));
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<bool>()
            .Produces(StatusCodes.Status404NotFound)
            .WithName("DeleteDepartment")
            .WithTags("Department");
    }
}
