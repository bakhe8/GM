using Xunit;

namespace GuaranteeManager.Tests
{
    [CollectionDefinition("Database")]
    public class DatabaseTestCollection : ICollectionFixture<TestEnvironmentFixture>
    {
    }
}
