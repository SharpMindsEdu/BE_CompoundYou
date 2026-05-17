using Application.Features.EmployeeSkills.Commands;
using Application.Shared.Services;
using Domain.Entities;
using Domain.Repositories;
using MediatR;

namespace Application.Features.EmployeeSkills.Handlers;

public class MatrixHubNotificationHandler(
    IMatrixNotificationService notificationService, 
    IRepository<Employee> employees) 
    : INotificationHandler<ValidateAssessment.AssessmentValidatedNotification>
{
    public async Task Handle(ValidateAssessment.AssessmentValidatedNotification notification, CancellationToken ct)
    {
        var assessment = notification.Assessment;
        var employee = await employees.GetById(assessment.EmployeeId);
        
        if (employee == null) return;

        await notificationService.NotifyAssessmentValidatedAsync(
            assessment.Id, 
            employee.UserId, 
            employee.TeamId, 
            ct);
    }
}
