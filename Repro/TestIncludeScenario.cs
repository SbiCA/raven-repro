using FluentAssertions;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.TestDriver;

namespace Repro;

public class TestIncludeScenario :RavenTestDriver
{
    private readonly IDocumentStore _documentStore;

    public TestIncludeScenario()
    {
        _documentStore = GetDocumentStore();
        using var session = _documentStore.OpenSession();
        session.Store(new Membership("Membership/1", "User/Ayende", "Event/Raven-Rocks"));
        session.Store(new Membership("Membership/2", "User/Simi", "Event/Raven-Rocks"));
        session.Store(new User("User/Ayende", "Oren"));
        session.Store(new User("User/Simi", "Simon"));
        session.Store(new Event("Event/Raven-Rocks", "RavenDb developer conference"));
        session.CountersFor("Event/Raven-Rocks").Increment("Members", 2);
        session.SaveChanges();
        _documentStore.ExecuteIndex(new Membership_ByEventAndUserId());
        WaitForIndexing(_documentStore);
    }
    [Fact]
    public void ShouldIncludeRelatedDocumentCounters()
    {
        WaitForUserToContinueTheTest(_documentStore);

        using var session = _documentStore.OpenSession();
        session.Advanced.MaxNumberOfRequestsPerSession = 1;
        var memberships = session.Query<Membership_ByEventAndUserId.Result,Membership_ByEventAndUserId>()
            .Where(m => m.UserId == "User/Ayende")
            .Include(m => m.EventId)
            .ToList();

        var @event = session.Load<Event>("Event/Raven-Rocks");
        // fails here ❌
        var numberOfMembers = session.CountersFor("Event/Raven-Rocks").Get("Members");

        memberships.Should().HaveCount(1);
        @event.Should().NotBeNull();
        numberOfMembers.Should().Be(2);
    }
}

public class Membership_ByEventAndUserId : AbstractIndexCreationTask<Membership>
{
    public class Result
    {
        public string UserId { get; set; }
        public string EventId { get; set; }
    }
    public Membership_ByEventAndUserId()
    {
        Map = members => from membership in members
            select new Result{ UserId = membership.UserId, EventId = membership.EventId};
    }
}
public record Membership(string Id, string UserId, string EventId);

public record Event(string Id, string Name);

public record User(string Id, string Name);