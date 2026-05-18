namespace Integration.Tests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationTestCollection : ICollectionFixture<IntegrationTestStackFixture>
{
    public const string Name = "DockerIntegrationTests";
}
