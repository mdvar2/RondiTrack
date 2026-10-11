namespace RondiTrack.Tests.TestSupport;

// One database per run; serialize classes and keep GUID-based test records independent.
[CollectionDefinition(Name, DisableParallelization = true)]
public class PostgresCollection : ICollectionFixture<PostgresApiFactory>
{
    public const string Name = "Postgres collection";
}
