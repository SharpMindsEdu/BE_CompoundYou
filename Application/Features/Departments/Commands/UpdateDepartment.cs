using Application.Authorization;
using Application.Features.Departments.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Departments.Commands;

public static class UpdateDepartment
{
    public const string Endpoint = "api/departments/{id:long}";

    public record UpdateDepartmentCommand(long Id, string Name, long? ParentDepartmentId)
        : ICommandRequest<Result<DepartmentDto>>,
            IAuditable
    {
        public string AuditAction => "department.update";
        public string AuditEntityType => nameof(Department);
        public long? AuditEntityId => Id;
    }

    public class Validator : AbstractValidator<UpdateDepartmentCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.ParentDepartmentId)
                .Must((cmd, parent) => parent != cmd.Id)
                .WithMessage("A department cannot be its own parent.");
        }
    }

    internal sealed class Handler(IRepository<Department> repo)
        : IRequestHandler<UpdateDepartmentCommand, Result<DepartmentDto>>
    {
        public async Task<Result<DepartmentDto>> Handle(
            UpdateDepartmentCommand request,
            CancellationToken ct
        )
        {
            var department = await repo.GetById(request.Id);
            if (department is null)
                return Result<DepartmentDto>.Failure(
                    TenancyErrors.DepartmentNotFound,
                    ResultStatus.NotFound
                );

            if (
                request.ParentDepartmentId is not null
                && !await repo.Exist(d => d.Id == request.ParentDepartmentId.Value, ct)
            )
                return Result<DepartmentDto>.Failure(
                    TenancyErrors.DepartmentNotFound,
                    ResultStatus.NotFound
                );

            department.Name = request.Name;
            department.ParentDepartmentId = request.ParentDepartmentId;
            department.UpdatedOn = DateTimeOffset.UtcNow;
            repo.Update(department);
            return Result<DepartmentDto>.Success(department);
        }
    }
}

public class UpdateDepartmentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                UpdateDepartment.Endpoint,
                async (long id, UpdateDepartment.UpdateDepartmentCommand body, ISender sender) =>
                {
                    var result = await sender.Send(body with { Id = id });
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<DepartmentDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("UpdateDepartment")
            .WithTags("Department");
    }
}
