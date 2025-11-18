using Application.Shared;
using Domain.Entities;
using Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Habits.Commands.HabitHistories;

public static class CreateHabitHistory
{
    public const string Endpoint = "api/habits/history";

    public record CreateHabitHistoryCommand(long? UserId) : ICommandRequest<Result<bool>>;

    internal sealed class Handler(
        IRepository<HabitTime> habitTimeRepo,
        IRepository<HabitHistory> historyRepo,
        ILogger<Handler> logger
    ) : IRequestHandler<CreateHabitHistoryCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(
            CreateHabitHistoryCommand request,
            CancellationToken ct
        )
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var tomorrow = today.AddDays(1);

                var dates = new[] { today, tomorrow };

                var dayOfWeeks = dates.Select(d => d.DayOfWeek).ToHashSet();
                var habitTimes = await habitTimeRepo.ListAll(
                    predicate: x =>
                        dayOfWeeks.Contains(x.Day)
                        && (request.UserId == null || x.UserId == request.UserId),
                    ct
                );

                var alreadyAdded = await historyRepo.ListAll(
                    selector: x => new { x.HabitTimeId, x.Date },
                    predicate: x => x.Date >= DateTime.UtcNow.Date,
                    ct
                );

                var alreadyAddedSet = alreadyAdded.Select(x => x.HabitTimeId).ToHashSet();

                var newEntries = new List<HabitHistory>();

                foreach (var habitTime in habitTimes)
                {
                    newEntries.AddRange(
                        from date in dates
                        where habitTime.Day == date.DayOfWeek
                        where !alreadyAddedSet.Contains((long?)habitTime.Id)
                        select new HabitHistory
                        {
                            Date = DateTime.SpecifyKind(date + habitTime.Time, DateTimeKind.Utc),
                            HabitId = habitTime.HabitId,
                            HabitTimeId = habitTime.Id,
                            UserId = habitTime.UserId,
                        }
                    );
                }

                if (newEntries.Count != 0)
                    await historyRepo.Add(newEntries.ToArray());

                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while creating habit history entries");
                return Result<bool>.Failure("Internal error while creating history entries.");
            }
        }
    }
}
