using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Domain.Entities;
using Domain.Entities.Chat;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Infrastructure;

public abstract partial class IntegrationTestBase
{
    private static readonly JsonSerializerOptions IntegrationJsonOptions =
        new(JsonSerializerDefaults.Web);


    protected static string WithQuery(string route, params (string Name, object? Value)[] parameters)
    {
        var query = parameters
            .Where(x => x.Value is not null)
            .Select(x =>
                $"{Uri.EscapeDataString(x.Name)}={Uri.EscapeDataString(ToQueryValue(x.Value))}"
            )
            .ToArray();

        return query.Length == 0 ? route : $"{route}?{string.Join("&", query)}";
    }

    private static string ToQueryValue(object? value) =>
        value switch
        {
            null => string.Empty,
            bool boolean => boolean ? "true" : "false",
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
        };

    protected async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpMethod method,
        string requestUri,
        string token,
        object? body = null,
        CancellationToken cancellationToken = default
    )
    {
        var request = CreateAuthorizedRequest(method, requestUri, token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: IntegrationJsonOptions);
        }

        return await Client.SendAsync(request, cancellationToken);
    }

    protected async Task<JsonElement> SendAuthorizedJsonAsync(
        HttpMethod method,
        string requestUri,
        string token,
        object? body = null,
        CancellationToken cancellationToken = default
    )
    {
        using var response = await SendAuthorizedAsync(method, requestUri, token, body, cancellationToken);
        return await ReadJsonElementAsync(response, cancellationToken);
    }

    protected static async Task<JsonElement> ReadJsonElementAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default
    )
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected success status code but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}"
        );

        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "null" : body);
        return document.RootElement.Clone();
    }

    protected static long GetRequiredLong(JsonElement json, string propertyName) =>
        json.GetProperty(propertyName).GetInt64();

    protected static string GetRequiredString(JsonElement json, string propertyName)
    {
        var value = json.GetProperty(propertyName).GetString();
        Assert.False(string.IsNullOrWhiteSpace(value));
        return value!;
    }

    protected static JsonElement GetPageItems(JsonElement page) => page.GetProperty("items");

    protected static void AssertArrayContainsId(JsonElement array, long id) =>
        AssertArrayContainsLongProperty(array, "id", id);

    protected static void AssertArrayContainsLongProperty(
        JsonElement array,
        string propertyName,
        long expectedValue
    )
    {
        Assert.Equal(JsonValueKind.Array, array.ValueKind);
        Assert.Contains(
            array.EnumerateArray(),
            item => GetRequiredLong(item, propertyName) == expectedValue
        );
    }

    protected static void AssertPageContainsId(JsonElement page, long id) =>
        AssertArrayContainsId(GetPageItems(page), id);

    protected async Task AssertEntityExistsAsync<TEntity>(
        long id,
        Tenant tenant,
        CancellationToken cancellationToken = default
    )
        where TEntity : class
    {
        await using var db = CreateDbContext(tenant.Id);
        var entity = await db.Set<TEntity>().FindAsync([id], cancellationToken);
        Assert.NotNull(entity);
    }

    protected async Task<GreenTenantContext> CreateTenantContextAsync(
        TenantRole role = TenantRole.Employee,
        bool createEmployee = true,
        CancellationToken cancellationToken = default
    )
    {
        var user = await SeedUserAsync(cancellationToken: cancellationToken);
        var tenant = await SeedTenantAsync(ownerUserId: user.Id, cancellationToken: cancellationToken);
        var membership = await SeedTenantMembershipAsync(
            tenant,
            user,
            role,
            cancellationToken: cancellationToken
        );
        var employee = createEmployee
            ? await SeedEmployeeAsync(tenant, user, cancellationToken: cancellationToken)
            : null;
        var token = await LoginAsync(user, cancellationToken);
        return new GreenTenantContext(tenant, user, membership, employee, token);
    }

    protected async Task<GreenPlatformAdminContext> CreatePlatformAdminContextAsync(
        CancellationToken cancellationToken = default
    )
    {
        var user = await SeedUserAsync(isPlatformAdmin: true, cancellationToken: cancellationToken);
        var token = await LoginAsync(user, cancellationToken);
        return new GreenPlatformAdminContext(user, token);
    }

    protected async Task<GreenTenantContext> CreateManagerWithReportAsync(
        CancellationToken cancellationToken = default
    )
    {
        var manager = await CreateTenantContextAsync(TenantRole.Manager, cancellationToken: cancellationToken);
        Assert.NotNull(manager.Employee);
        await SeedEmployeeAsync(
            manager.Tenant,
            manager: manager.Employee,
            cancellationToken: cancellationToken
        );
        return manager;
    }

    protected async Task<SkillCategory> SeedSkillCategoryAsync(
        Tenant tenant,
        string? name = null,
        string? description = null,
        bool isActive = true,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = CreateDbContext(tenant.Id);
        var category = new SkillCategory
        {
            TenantId = tenant.Id,
            Name = name ?? UniqueName("Skill Category"),
            Description = description ?? "Seeded integration skill category",
            IsActive = isActive,
        };

        db.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return category;
    }

    protected async Task<SkillLevel> SeedSkillLevelAsync(
        Tenant tenant,
        int order = 1,
        string? name = null,
        string? description = null,
        int? pointsThreshold = null,
        bool isActive = true,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = CreateDbContext(tenant.Id);
        var level = new SkillLevel
        {
            TenantId = tenant.Id,
            SkillId = null,
            Order = order,
            Name = name ?? $"Level {order}",
            Description = description ?? $"Seeded level {order}",
            PointsThreshold = pointsThreshold ?? (order - 1) * 100,
            IsActive = isActive,
        };

        db.Add(level);
        await db.SaveChangesAsync(cancellationToken);
        return level;
    }

    protected async Task<Skill> SeedSkillAsync(
        Tenant tenant,
        SkillCategory? category = null,
        Skill? parentSkill = null,
        string? name = null,
        string? description = null,
        bool isActive = true,
        CancellationToken cancellationToken = default
    )
    {
        category ??= await SeedSkillCategoryAsync(tenant, cancellationToken: cancellationToken);

        await using var db = CreateDbContext(tenant.Id);
        var skill = new Skill
        {
            TenantId = tenant.Id,
            SkillCategoryId = category.Id,
            Name = name ?? UniqueName("Skill"),
            Description = description ?? "Seeded integration skill",
            ParentSkillId = parentSkill?.Id,
            IsActive = isActive,
        };

        db.Add(skill);
        await db.SaveChangesAsync(cancellationToken);
        return skill;
    }

    protected async Task<JobFamily> SeedJobFamilyAsync(
        Tenant tenant,
        string? name = null,
        string? description = null,
        bool isActive = true,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = CreateDbContext(tenant.Id);
        var jobFamily = new JobFamily
        {
            TenantId = tenant.Id,
            Name = name ?? UniqueName("Job Family"),
            Description = description ?? "Seeded integration job family",
            IsActive = isActive,
        };

        db.Add(jobFamily);
        await db.SaveChangesAsync(cancellationToken);
        return jobFamily;
    }

    protected async Task<CareerLevel> SeedCareerLevelAsync(
        Tenant tenant,
        JobFamily? jobFamily = null,
        decimal order = 1m,
        string? name = null,
        string? description = null,
        CancellationToken cancellationToken = default
    )
    {
        jobFamily ??= await SeedJobFamilyAsync(tenant, cancellationToken: cancellationToken);

        await using var db = CreateDbContext(tenant.Id);
        var level = new CareerLevel
        {
            TenantId = tenant.Id,
            JobFamilyId = jobFamily.Id,
            Order = order,
            Name = name ?? $"Career Level {order}",
            Description = description ?? "Seeded integration career level",
        };

        db.Add(level);
        await db.SaveChangesAsync(cancellationToken);
        return level;
    }

    protected async Task<RoleProfile> SeedRoleProfileAsync(
        Tenant tenant,
        JobFamily? jobFamily = null,
        CareerLevel? careerLevel = null,
        string? name = null,
        string? description = null,
        bool isActive = true,
        CancellationToken cancellationToken = default
    )
    {
        jobFamily ??= await SeedJobFamilyAsync(tenant, cancellationToken: cancellationToken);
        careerLevel ??= await SeedCareerLevelAsync(
            tenant,
            jobFamily,
            cancellationToken: cancellationToken
        );

        await using var db = CreateDbContext(tenant.Id);
        var roleProfile = new RoleProfile
        {
            TenantId = tenant.Id,
            JobFamilyId = jobFamily.Id,
            CareerLevelId = careerLevel.Id,
            Name = name ?? UniqueName("Role Profile"),
            Description = description ?? "Seeded integration role profile",
            IsActive = isActive,
        };

        db.Add(roleProfile);
        await db.SaveChangesAsync(cancellationToken);
        return roleProfile;
    }

    protected async Task<RoleProfileSkillRequirement> SeedRoleProfileSkillRequirementAsync(
        Tenant tenant,
        RoleProfile roleProfile,
        Skill skill,
        SkillLevel requiredSkillLevel,
        decimal weight = 1m,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = CreateDbContext(tenant.Id);
        var requirement = new RoleProfileSkillRequirement
        {
            TenantId = tenant.Id,
            RoleProfileId = roleProfile.Id,
            SkillId = skill.Id,
            RequiredSkillLevelId = requiredSkillLevel.Id,
            Weight = weight,
        };

        db.Add(requirement);
        await db.SaveChangesAsync(cancellationToken);
        return requirement;
    }

    protected async Task<TeamSkillRequirement> SeedTeamSkillRequirementAsync(
        Tenant tenant,
        Team team,
        Skill skill,
        SkillLevel requiredSkillLevel,
        int weight = 1,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = CreateDbContext(tenant.Id);
        var requirement = new TeamSkillRequirement
        {
            TenantId = tenant.Id,
            TeamId = team.Id,
            SkillId = skill.Id,
            RequiredSkillLevelId = requiredSkillLevel.Id,
            Weight = weight,
        };

        db.Add(requirement);
        await db.SaveChangesAsync(cancellationToken);
        return requirement;
    }

    protected async Task<EmployeeRoleProfile> SeedEmployeeRoleProfileAsync(
        Tenant tenant,
        Employee employee,
        RoleProfile roleProfile,
        bool isActive = true,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = CreateDbContext(tenant.Id);
        var assignment = new EmployeeRoleProfile
        {
            TenantId = tenant.Id,
            EmployeeId = employee.Id,
            RoleProfileId = roleProfile.Id,
            IsActive = isActive,
        };

        db.Add(assignment);
        await db.SaveChangesAsync(cancellationToken);
        return assignment;
    }

    protected async Task<EmployeeSkillAssessment> SeedEmployeeSkillAssessmentAsync(
        Tenant tenant,
        Employee employee,
        Skill skill,
        SkillLevel claimedSkillLevel,
        SkillLevel? validatedSkillLevel = null,
        Employee? validatedBy = null,
        SkillAssessmentStatus status = SkillAssessmentStatus.Validated,
        string? evidence = null,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = CreateDbContext(tenant.Id);
        var assessment = new EmployeeSkillAssessment
        {
            TenantId = tenant.Id,
            EmployeeId = employee.Id,
            SkillId = skill.Id,
            ClaimedSkillLevelId = claimedSkillLevel.Id,
            ValidatedSkillLevelId = validatedSkillLevel?.Id,
            ValidatedByEmployeeId = validatedBy?.Id,
            ValidatedOn = validatedSkillLevel is null ? null : DateTimeOffset.UtcNow,
            Status = status,
            Evidence = evidence ?? "Seeded integration evidence",
        };

        db.Add(assessment);
        await db.SaveChangesAsync(cancellationToken);
        return assessment;
    }

    protected async Task<CareerPathSnapshot> SeedCareerPathSnapshotAsync(
        Tenant tenant,
        Employee employee,
        RoleProfile? currentRoleProfile = null,
        RoleProfile? targetRoleProfile = null,
        int readinessScore = 75,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = CreateDbContext(tenant.Id);
        var snapshot = new CareerPathSnapshot
        {
            TenantId = tenant.Id,
            EmployeeId = employee.Id,
            CurrentRoleProfileId = currentRoleProfile?.Id,
            TargetRoleProfileId = targetRoleProfile?.Id,
            ReadinessScore = readinessScore,
            SkillFitScore = readinessScore,
            ValidationCoverageScore = readinessScore,
            GoalCompletionScore = null,
            Band = CareerReadinessBand.Developing,
        };

        db.Add(snapshot);
        await db.SaveChangesAsync(cancellationToken);
        return snapshot;
    }

    protected async Task<TenantInvitation> SeedTenantInvitationAsync(
        Tenant tenant,
        string? email = null,
        TenantRole role = TenantRole.Employee,
        string? token = null,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = CreateDbContext();
        var invitation = new TenantInvitation
        {
            TenantId = tenant.Id,
            Email = email ?? UniqueEmail("invite"),
            Role = role,
            Token = token ?? UniqueSlug("invite-token"),
            ExpiresOn = DateTimeOffset.UtcNow.AddDays(7),
        };

        db.Add(invitation);
        await db.SaveChangesAsync(cancellationToken);
        return invitation;
    }

    protected async Task<AuditLogEntry> SeedAuditLogEntryAsync(
        Tenant tenant,
        User? actor = null,
        string action = "integration.seed",
        string entityType = "IntegrationEntity",
        long? entityId = null,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = CreateDbContext(tenant.Id);
        var entry = new AuditLogEntry
        {
            TenantId = tenant.Id,
            ActorUserId = actor?.Id,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            MetadataJson = "{\"source\":\"integration\"}",
        };

        db.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return entry;
    }

    protected async Task<ExceptionLog> SeedExceptionLogAsync(
        string? exceptionType = null,
        string? message = null,
        bool isHandled = false,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = CreateDbContext();
        var log = new ExceptionLog
        {
            ExceptionType = exceptionType ?? "IntegrationException",
            Message = message ?? UniqueName("Seeded exception"),
            StackTrace = "at Integration.Tests.GreenPath()",
            Source = "Integration.Tests",
            CaptureKind = "test",
            IsHandled = isHandled,
            RequestPath = "/integration",
            RequestMethod = "GET",
            TraceId = Guid.NewGuid().ToString("N"),
            UserIdentifier = "integration",
            MetadataJson = "{\"source\":\"integration\"}",
        };

        db.Add(log);
        await db.SaveChangesAsync(cancellationToken);
        return log;
    }

    protected async Task<ChatRoom> SeedChatRoomAsync(
        string? name = null,
        bool isPublic = false,
        bool isDirect = false,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = CreateDbContext();
        var room = new ChatRoom
        {
            Name = name ?? UniqueName("Chat Room"),
            IsPublic = isPublic,
            IsDirect = isDirect,
        };

        db.Add(room);
        await db.SaveChangesAsync(cancellationToken);
        return room;
    }

    protected async Task<ChatRoomUser> SeedChatRoomUserAsync(
        ChatRoom room,
        User user,
        bool isAdmin = false,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = CreateDbContext();
        var chatUser = new ChatRoomUser
        {
            ChatRoomId = room.Id,
            UserId = user.Id,
            IsAdmin = isAdmin,
        };

        db.Add(chatUser);
        await db.SaveChangesAsync(cancellationToken);
        return chatUser;
    }

    protected async Task<ChatMessage> SeedChatMessageAsync(
        ChatRoom room,
        User user,
        string? content = null,
        string? attachmentUrl = null,
        AttachmentType? attachmentType = null,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = CreateDbContext();
        var message = new ChatMessage
        {
            ChatRoomId = room.Id,
            UserId = user.Id,
            Content = content ?? UniqueName("Integration message"),
            AttachmentUrl = attachmentUrl,
            AttachmentType = attachmentType,
        };

        db.Add(message);
        await db.SaveChangesAsync(cancellationToken);
        return message;
    }

    protected async Task<Employee> GetFirstDirectReportAsync(
        GreenTenantContext manager,
        CancellationToken ct
    )
    {
        Assert.NotNull(manager.Employee);
        await using var db = CreateDbContext(manager.Tenant.Id);
        var report = await db.Set<Employee>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ManagerEmployeeId == manager.Employee.Id, ct);
        Assert.NotNull(report);
        return report!;
    }

    protected async Task SetEmployeeTeamAsync(
        Tenant tenant,
        Employee employee,
        Team team,
        CancellationToken ct
    )
    {
        await using var db = CreateDbContext(tenant.Id);
        var row = await db.Set<Employee>().FindAsync([employee.Id], ct);
        Assert.NotNull(row);
        row.TeamId = team.Id;
        await db.SaveChangesAsync(ct);
    }

    protected async Task<CareerPathSeed> SeedCareerPathDataAsync(
        Tenant tenant,
        Employee employee,
        Employee? validator,
        CancellationToken ct
    )
    {
        var family = await SeedJobFamilyAsync(tenant, cancellationToken: ct);
        var currentLevel = await SeedCareerLevelAsync(
            tenant,
            family,
            order: 1m,
            name: UniqueName("Associate"),
            cancellationToken: ct
        );
        var targetLevel = await SeedCareerLevelAsync(
            tenant,
            family,
            order: 2m,
            name: UniqueName("Senior"),
            cancellationToken: ct
        );
        var currentRole = await SeedRoleProfileAsync(
            tenant,
            family,
            currentLevel,
            name: UniqueName("Current Role"),
            cancellationToken: ct
        );
        var targetRole = await SeedRoleProfileAsync(
            tenant,
            family,
            targetLevel,
            name: UniqueName("Target Role"),
            cancellationToken: ct
        );
        await SeedEmployeeRoleProfileAsync(tenant, employee, currentRole, cancellationToken: ct);

        var skill = await SeedSkillAsync(tenant, cancellationToken: ct);
        var actualLevel = await SeedSkillLevelAsync(tenant, order: 1, cancellationToken: ct);
        var requiredLevel = await SeedSkillLevelAsync(tenant, order: 2, cancellationToken: ct);
        await SeedRoleProfileSkillRequirementAsync(
            tenant,
            targetRole,
            skill,
            requiredLevel,
            cancellationToken: ct
        );
        await SeedEmployeeSkillAssessmentAsync(
            tenant,
            employee,
            skill,
            actualLevel,
            validatedSkillLevel: actualLevel,
            validatedBy: validator,
            status: SkillAssessmentStatus.Validated,
            cancellationToken: ct
        );

        return new CareerPathSeed(currentRole, targetRole, skill, actualLevel, requiredLevel);
    }

    protected async Task<ManagerAssessmentSeed> SeedManagerAssessmentAsync(
        SkillAssessmentStatus status,
        CancellationToken ct
    )
    {
        var manager = await CreateTenantContextAsync(TenantRole.Manager, cancellationToken: ct);
        Assert.NotNull(manager.Employee);
        var employee = await SeedEmployeeAsync(
            manager.Tenant,
            manager: manager.Employee,
            cancellationToken: ct
        );
        var skill = await SeedSkillAsync(manager.Tenant, cancellationToken: ct);
        var claimedLevel = await SeedSkillLevelAsync(manager.Tenant, order: 1, cancellationToken: ct);
        var validatedLevel = await SeedSkillLevelAsync(manager.Tenant, order: 2, cancellationToken: ct);
        var assessment = await SeedEmployeeSkillAssessmentAsync(
            manager.Tenant,
            employee,
            skill,
            claimedLevel,
            validatedSkillLevel: status == SkillAssessmentStatus.Validated ? validatedLevel : null,
            validatedBy: status == SkillAssessmentStatus.Validated ? manager.Employee : null,
            status: status,
            cancellationToken: ct
        );

        return new ManagerAssessmentSeed(manager, employee, assessment, validatedLevel);
    }

    protected sealed record CareerPathSeed(
        RoleProfile CurrentRole,
        RoleProfile TargetRole,
        Skill Skill,
        SkillLevel ActualLevel,
        SkillLevel RequiredLevel
    );

    protected sealed record ManagerAssessmentSeed(
        GreenTenantContext Manager,
        Employee Employee,
        EmployeeSkillAssessment Assessment,
        SkillLevel ValidatedLevel
    );

    protected async Task<string> UploadSeedAttachmentAsync(
        string token,
        CancellationToken cancellationToken = default
    )
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Post, "api/media", token);
        using var content = new MultipartFormDataContent();
        var bytes = Encoding.UTF8.GetBytes("integration attachment");
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "integration.png");
        request.Content = content;

        using var response = await Client.SendAsync(request, cancellationToken);
        var json = await ReadJsonElementAsync(response, cancellationToken);
        return GetRequiredString(json, "path");
    }

    protected sealed record GreenTenantContext(
        Tenant Tenant,
        User User,
        TenantMembership Membership,
        Employee? Employee,
        string Token
    );

    protected sealed record GreenPlatformAdminContext(User User, string Token);
}
