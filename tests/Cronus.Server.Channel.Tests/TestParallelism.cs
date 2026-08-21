using Xunit;

// Many tests here pump two encrypted MapleSessions over in-memory pipes concurrently. Running too
// many such tests at once starves the CPU and can trip a test's timeout even though it passes in
// isolation. Cap the parallelism so the suite stays reliable under load without giving up all of it.
[assembly: CollectionBehavior(MaxParallelThreads = 4)]
