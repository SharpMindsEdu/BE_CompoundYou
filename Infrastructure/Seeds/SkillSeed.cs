using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Seeds;

/// <summary>
/// Seeds the global (TenantId == null) Skill catalog: four broad
/// SkillCategory rows + ~25 commonly used Skills with a 3-tier scale
/// (Beginner / Advanced / Expert). Idempotent — runs once when the
/// SkillCategory table is empty, no-ops thereafter.
///
/// Must be invoked with a tenant context that has IsPlatformAdmin=true,
/// because the TenantStampingInterceptor only permits TenantId=null
/// inserts under that condition.
/// </summary>
public static class SkillSeed
{
    private static readonly string[] LevelNames = ["Beginner", "Advanced", "Expert"];
    private static readonly int[] LevelThresholds = [0, 100, 300];

    public static void EnsureSeeded(ApplicationDbContext db)
    {
        if (db.Set<SkillCategory>().Any())
            return;

        var technical = new SkillCategory
        {
            Name = "Technical",
            Description = "Engineering, programming, infrastructure, and technical disciplines.",
        };
        var softSkills = new SkillCategory
        {
            Name = "Soft Skills",
            Description = "Communication, collaboration, and interpersonal effectiveness.",
        };
        var leadership = new SkillCategory
        {
            Name = "Leadership",
            Description = "People management, decision-making, and organizational impact.",
        };
        var compliance = new SkillCategory
        {
            Name = "Compliance",
            Description = "Regulatory, security, privacy, and process compliance.",
        };

        db.AddRange(technical, softSkills, leadership, compliance);
        db.SaveChanges();

        SkillSeedDefinition[] skills =
        [
            new("C#", technical, "Object-oriented and functional programming in C#/.NET."),
            new("TypeScript", technical, "Static typing on the JavaScript ecosystem."),
            new("React", technical, "Component-based UI development with React."),
            new("Angular", technical, "Enterprise web apps with Angular."),
            new("PostgreSQL", technical, "Relational data modelling and query optimisation in Postgres."),
            new("Docker", technical, "Container packaging, networking, and Compose."),
            new("Kubernetes", technical, "Container orchestration and cluster operations."),
            new("Cloud Architecture", technical, "Designing scalable, resilient cloud systems (AWS/Azure/GCP)."),
            new("CI/CD", technical, "Pipelines, automated build, test, and deployment."),
            new("System Design", technical, "Designing distributed systems at scale."),
            new("Data Analysis", technical, "Extracting insight from structured and unstructured data."),
            new("Machine Learning", technical, "Building and evaluating ML models."),

            new("Communication", softSkills, "Clear written and verbal communication."),
            new("Active Listening", softSkills, "Hearing intent, asking the right follow-ups."),
            new("Collaboration", softSkills, "Working effectively across roles and disciplines."),
            new("Presentation", softSkills, "Structuring and delivering compelling talks."),
            new("Conflict Resolution", softSkills, "De-escalating disagreements and finding common ground."),
            new("Time Management", softSkills, "Prioritising and delivering within deadlines."),

            new("People Management", leadership, "Hiring, growing, and retaining strong teams."),
            new("Strategic Thinking", leadership, "Translating vision into actionable strategy."),
            new("Decision Making", leadership, "Making timely, well-reasoned calls under uncertainty."),
            new("Coaching", leadership, "Supporting growth through feedback and questions."),
            new("Change Management", leadership, "Leading teams through transitions and ambiguity."),

            new("GDPR", compliance, "EU data protection regulation and obligations."),
            new("Information Security", compliance, "ISO 27001 / SOC 2 practices and controls."),
            new("Code of Conduct", compliance, "Ethics and acceptable-use policies."),
            new("Anti-Bribery", compliance, "Recognising and preventing corruption."),
        ];

        foreach (var seed in skills)
        {
            var skill = new Skill
            {
                Name = seed.Name,
                Description = seed.Description,
                SkillCategoryId = seed.Category.Id,
                IsActive = true,
            };
            db.Add(skill);
            db.SaveChanges();

            for (var i = 0; i < LevelNames.Length; i++)
            {
                db.Add(new SkillLevel
                {
                    SkillId = skill.Id,
                    Order = i + 1,
                    Name = LevelNames[i],
                    PointsThreshold = LevelThresholds[i],
                });
            }
        }

        db.SaveChanges();
    }

    private sealed record SkillSeedDefinition(string Name, SkillCategory Category, string Description);
}
