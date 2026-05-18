namespace Integration.Tests;

public static class ServiceTestCategories
{
    private const string DefaultSuffix = "_integration_tests";

    public const string IntegrationTests = "integration_tests";
    public const string UserTests = "user" + DefaultSuffix;
    public const string ChatTests = "chat" + DefaultSuffix;
    public const string DepartmentTests = "department" + DefaultSuffix;
    public const string TeamTests = "team" + DefaultSuffix;
    public const string TenantTests = "tenant" + DefaultSuffix;
    public const string TenantMembershipTests = "tenant_membership" + DefaultSuffix;
    public const string EmployeeTests = "employee" + DefaultSuffix;
    public const string EmployeeSkillTests = "employee_skill" + DefaultSuffix;
    public const string CareerTests = "career" + DefaultSuffix;
    public const string SkillTests = "skill" + DefaultSuffix;
    public const string AuditTests = "audit" + DefaultSuffix;
    public const string GdprTests = "gdpr" + DefaultSuffix;
    public const string MediaTests = "media" + DefaultSuffix;
    public const string DiagnosticsTests = "diagnostics" + DefaultSuffix;
}
