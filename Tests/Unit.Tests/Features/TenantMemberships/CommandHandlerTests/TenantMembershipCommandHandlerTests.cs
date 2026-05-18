using Application.Features.TenantMemberships.Commands;
using Application.Shared;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.TenantMemberships.CommandHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.TenantMembershipTests)]
public sealed class InviteUserCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task InviteUser_WithTenantAdminContext_ShouldCreateInvitation()
    {
        var tenant = SeedTenant();
        SetTenantContext(tenant.Id);

        var result = await Send(
            new InviteUser.InviteUserCommand(tenant.Id, "invitee@example.com", TenantRole.Employee),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal(tenant.Id, result.Data!.TenantId);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.Token));
        Assert.True(result.Data.ExpiresOn > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task InviteUser_ForExistingActiveMember_ShouldReturnConflict()
    {
        var tenant = SeedTenant();
        var user = SeedUser(email: "member@example.com");
        PersistWithDatabase(db =>
            db.Add(
                new TenantMembership
                {
                    TenantId = tenant.Id,
                    UserId = user.Id,
                    Role = TenantRole.Employee,
                    IsActive = true,
                }
            )
        );
        SetTenantContext(tenant.Id);

        var result = await Send(
            new InviteUser.InviteUserCommand(tenant.Id, user.Email!, TenantRole.Manager),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal(TenancyErrors.MembershipAlreadyExists, result.ErrorMessage);
    }

    [Fact]
    public async Task InviteUser_WithInvalidEmail_ShouldThrowValidationException()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(
                new InviteUser.InviteUserCommand(1, "not-an-email", TenantRole.Employee),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("Email", ex.Message);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.TenantMembershipTests)]
public sealed class AcceptInviteCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task AcceptInvite_WithValidInvitation_ShouldCreateMembershipAndMarkAccepted()
    {
        var tenant = SeedTenant();
        var user = SeedUser();
        var invitation = new TenantInvitation
        {
            TenantId = tenant.Id,
            Email = "invitee@example.com",
            Role = TenantRole.Manager,
            Token = "token-123",
            ExpiresOn = DateTimeOffset.UtcNow.AddDays(1),
        };
        PersistWithDatabase(db => db.Add(invitation));

        var result = await Send(
            new AcceptInvite.AcceptInviteCommand(invitation.Token, user.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal(TenantRole.Manager, result.Data!.Role);
        Assert.True(result.Data.IsActive);

        WithDatabase(db =>
        {
            var storedInvite = db.Set<TenantInvitation>().Single();
            Assert.NotNull(storedInvite.AcceptedOn);
            Assert.Equal(user.Id, storedInvite.AcceptedByUserId);
        });
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.TenantMembershipTests)]
public sealed class ChangeRoleCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task ChangeRole_ForOwnMembership_ShouldReturnConflict()
    {
        var tenant = SeedTenant();
        var user = SeedUser();
        TenantMembership membership = null!;
        PersistWithDatabase(db =>
        {
            membership = new TenantMembership
            {
                TenantId = tenant.Id,
                UserId = user.Id,
                Role = TenantRole.TenantAdmin,
                IsActive = true,
            };
            db.Add(membership);
        });
        SetTenantContext(tenant.Id, user.Id, membership.Id, TenantRole.TenantAdmin);

        var result = await Send(
            new ChangeRole.ChangeRoleCommand(
                tenant.Id,
                membership.Id,
                TenantRole.Employee,
                membership.Id
            ),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal(TenancyErrors.CannotChangeOwnRole, result.ErrorMessage);
    }

    [Fact]
    public async Task ChangeRole_ForAnotherMembership_ShouldUpdateRole()
    {
        var tenant = SeedTenant();
        var admin = SeedUser("Admin");
        var member = SeedUser("Member");
        TenantMembership adminMembership = null!;
        TenantMembership memberMembership = null!;
        PersistWithDatabase(db =>
        {
            adminMembership = new TenantMembership
            {
                TenantId = tenant.Id,
                UserId = admin.Id,
                Role = TenantRole.TenantAdmin,
                IsActive = true,
            };
            memberMembership = new TenantMembership
            {
                TenantId = tenant.Id,
                UserId = member.Id,
                Role = TenantRole.Employee,
                IsActive = true,
            };
            db.AddRange(adminMembership, memberMembership);
        });
        SetTenantContext(tenant.Id, admin.Id, adminMembership.Id, TenantRole.TenantAdmin);

        var result = await Send(
            new ChangeRole.ChangeRoleCommand(
                tenant.Id,
                memberMembership.Id,
                TenantRole.Manager,
                adminMembership.Id
            ),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal(TenantRole.Manager, result.Data!.Role);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.TenantMembershipTests)]
public sealed class RemoveMemberCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task RemoveMember_ShouldDeactivateMembership()
    {
        var tenant = SeedTenant();
        var admin = SeedUser("Admin");
        var member = SeedUser("Member");
        TenantMembership adminMembership = null!;
        TenantMembership memberMembership = null!;
        PersistWithDatabase(db =>
        {
            adminMembership = new TenantMembership
            {
                TenantId = tenant.Id,
                UserId = admin.Id,
                Role = TenantRole.TenantAdmin,
                IsActive = true,
            };
            memberMembership = new TenantMembership
            {
                TenantId = tenant.Id,
                UserId = member.Id,
                Role = TenantRole.Employee,
                IsActive = true,
            };
            db.AddRange(adminMembership, memberMembership);
        });
        SetTenantContext(tenant.Id, admin.Id, adminMembership.Id, TenantRole.TenantAdmin);

        var result = await Send(
            new RemoveMember.RemoveMemberCommand(tenant.Id, memberMembership.Id, adminMembership.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        WithDatabase(db => Assert.False(db.Set<TenantMembership>().Single(x => x.Id == memberMembership.Id).IsActive));
    }
}
