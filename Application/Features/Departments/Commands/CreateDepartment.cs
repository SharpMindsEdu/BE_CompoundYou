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

public static class CreateDepartment
{
    public const string Endpoint = "api/departments";

    public record CreateDepartmentCommand(string Name, long? ParentDepartmentId)
        : ICommandRequest<Result<DepartmentDto>>,
            IAuditable
    {
        public string AuditAction => "department.create";
        public string AuditEntityType => nameof(Department);
        public long? AuditEntityId => null;
    }

    public class Validator : AbstractValidator<CreateDepartmentCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        }
    }

    internal sealed class Handler(IRepository<Department> repo)
        : IRequestHandler<CreateDepartmentCommand, Result<DepartmentDto>>
    {
        public async Task<Result<DepartmentDto>> Handle(
            CreateDepartmentCommand request,
            CancellationToken ct
        )
        {
            if (
                request.ParentDepartmentId is not null
                && !await repo.Exist(d => d.Id == request.ParentDepartmentId.Value, ct)
            )
                return Result<DepartmentDto>.Failure(
                    TenancyErrors.DepartmentNotFound,
                    ResultStatus.NotFound
                );

            var entity = new Department { Name = request.Name, ParentDepartmentId = request.ParentDepartmentId };
            await repo.Add(entity);
            return Result<DepartmentDto>.Success(entity);
        }
    }
}

public class CreateDepartmentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                CreateDepartment.Endpoint,
                async (CreateDepartment.CreateDepartmentCommand cmd, ISender sender) =>
                {
                    var result = await sender.Send(cmd);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<DepartmentDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("CreateDepartment")
            .WithTags("Department");
    }
}
