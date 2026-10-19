using DsgWeb.Tests.Infrastructure;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace DsgWeb.Tests;

[CollectionDefinition("dsg")]
public class DsgCollection : ICollectionFixture<MosquittoFixture>;
