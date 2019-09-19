using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shouldly;
using Xunit;

namespace EF.Auditor.Tests
{
    public class AuditorTests
    {
        readonly AuditorTestsContext _context;
        readonly IAuditor _auditor;

        public AuditorTests()
        {
            _context = new AuditorTestsContext();
            _auditor = new Auditor(_context);
        }

        [Fact]
        public void WhenGettingDDDAuditLogsWithBifurcateChanges_ThenNewEntitiesAreIncludedCorrectly()
        {
            var author = new Person() { FirstName = "Saeb", LastName = "Amini" };
            _context.Add(author);

            var auditLogs = _auditor.GetLogs<TestAggregateRootBase>(ChangeSnapshotType.Bifurcate);
            auditLogs.Count.ShouldBe(1);
            var log = auditLogs.Single();
            log.ChangeType.ShouldBe(AuditLogChangeType.Added);
            var changeSnapshot = JsonConvert.DeserializeObject<JObject>(log.ChangeSnapshot);
            var before = changeSnapshot["Before"];
            before.Children<JProperty>().Count().ShouldBe(0); // no before details for a new entity
            var after = changeSnapshot["After"];
            after.Children<JProperty>().Count().ShouldNotBe(0);
            var loggedAfter = after.ToString().DeserializeTo<Person>();
            loggedAfter.FirstName.ShouldBe(author.FirstName);
            loggedAfter.LastName.ShouldBe(author.LastName);
        }

        [Fact]
        public void WhenGettingDDDAuditLogsWithInlineChanges_ThenNewEntitiesAreIncludedCorrectly()
        {
            var author = new Person() { FirstName = "Saeb", LastName = "Amini" };
            _context.Add(author);

            var auditLogs = _auditor.GetLogs<TestAggregateRootBase>(ChangeSnapshotType.Inline);
            auditLogs.Count.ShouldBe(1);
            var log = auditLogs.Single();
            log.ChangeType.ShouldBe(AuditLogChangeType.Added);
            var changeSnapshot = JsonConvert.DeserializeObject<JObject>(log.ChangeSnapshot);
            changeSnapshot[nameof(Person.FirstName)]["Before"].Value<string>().ShouldBeNull();
            changeSnapshot[nameof(Person.FirstName)]["After"].Value<string>().ShouldBe(author.FirstName);
            changeSnapshot[nameof(Person.LastName)]["Before"].Value<string>().ShouldBeNull();
            changeSnapshot[nameof(Person.LastName)]["After"].Value<string>().ShouldBe(author.LastName);
        }

        [Fact]
        public void WhenGettingDDDAuditLogsWithBifurcateChanges_ThenDeletedEntitiesAreIncludedCorrectly()
        {
            var author = new Person() { FirstName = "Saeb", LastName = "Amini" };
            _context.Add(author);
            _context.SaveChanges();
            _context.Remove(author);

            var auditLogs = _auditor.GetLogs<TestAggregateRootBase>(ChangeSnapshotType.Bifurcate);
            auditLogs.Count.ShouldBe(1);
            var log = auditLogs.Single();
            log.ChangeType.ShouldBe(AuditLogChangeType.Deleted);
            var changeSnapshot = JsonConvert.DeserializeObject<JObject>(log.ChangeSnapshot);
            var after = changeSnapshot["After"]; // no before details for a deleted entity
            after.Children<JProperty>().Count().ShouldBe(0);
            var before = changeSnapshot["Before"];
            before.Children<JProperty>().Count().ShouldNotBe(0);
            var loggedBefore = before.ToString().DeserializeTo<Person>();
            loggedBefore.FirstName.ShouldBe(author.FirstName);
            loggedBefore.LastName.ShouldBe(author.LastName);
        }

        [Fact]
        public void WhenGettingDDDAuditLogsWithInlineChanges_ThenDeletedEntitiesAreIncludedCorrectly()
        {
            var author = new Person() { FirstName = "Saeb", LastName = "Amini" };
            _context.Add(author);
            _context.SaveChanges();
            _context.Remove(author);

            var auditLogs = _auditor.GetLogs<TestAggregateRootBase>(ChangeSnapshotType.Inline);
            auditLogs.Count.ShouldBe(1);
            var log = auditLogs.Single();
            log.ChangeType.ShouldBe(AuditLogChangeType.Deleted);
            var changeSnapshot = JsonConvert.DeserializeObject<JObject>(log.ChangeSnapshot);
            changeSnapshot[nameof(Person.FirstName)]["Before"].Value<string>().ShouldBe(author.FirstName);
            changeSnapshot[nameof(Person.FirstName)]["After"].Value<string>().ShouldBeNull();
            changeSnapshot[nameof(Person.LastName)]["Before"].Value<string>().ShouldBe(author.LastName);
            changeSnapshot[nameof(Person.LastName)]["After"].Value<string>().ShouldBeNull();
        }

        [Fact]
        public void WhenGettingDDDAuditLogsWithBifurcateChanges_ThenModifiedEntitiesAreIncludedCorrectly()
        {
            var author = new Person() { FirstName = "Saeb", LastName = "Amini" };
            _context.Add(author);
            _context.SaveChanges();
            author.FirstName = "Kaiser";

            var auditLogs = _auditor.GetLogs<TestAggregateRootBase>(ChangeSnapshotType.Bifurcate);
            auditLogs.Count.ShouldBe(1);
            var log = auditLogs.Single();
            log.ChangeType.ShouldBe(AuditLogChangeType.Modified);
            var changeSnapshot = JsonConvert.DeserializeObject<JObject>(log.ChangeSnapshot);
            var before = changeSnapshot["Before"];
            before.Children<JProperty>().Count().ShouldBe(1);
            before[nameof(Person.FirstName)].Value<string>().ShouldBe("Saeb");
            before[nameof(Person.LastName)].ShouldBeNull(); // last name wasn't modified so shouldn't be included
            var after = changeSnapshot["After"];
            after.Children<JProperty>().Count().ShouldBe(1);
            after[nameof(Person.FirstName)].Value<string>().ShouldBe("Kaiser");
            after[nameof(Person.LastName)].ShouldBeNull(); // last name wasn't modified so shouldn't be included
        }

        [Fact]
        public void WhenGettingDDDAuditLogsWithInlineChanges_ThenModifiedEntitiesAreIncludedCorrectly()
        {
            var author = new Person() { FirstName = "Saeb", LastName = "Amini" };
            _context.Add(author);
            _context.SaveChanges();
            author.FirstName = "Kaiser";

            var auditLogs = _auditor.GetLogs<TestAggregateRootBase>(ChangeSnapshotType.Inline);
            auditLogs.Count.ShouldBe(1);
            var log = auditLogs.Single();
            log.ChangeType.ShouldBe(AuditLogChangeType.Modified);
            var changeSnapshot = JsonConvert.DeserializeObject<JObject>(log.ChangeSnapshot);
            changeSnapshot[nameof(Person.FirstName)]["Before"].Value<string>().ShouldBe("Saeb");
            changeSnapshot[nameof(Person.FirstName)]["After"].Value<string>().ShouldBe("Kaiser");
            changeSnapshot[nameof(Person.LastName)].ShouldBeNull(); // last name wasn't modified so shouldn't be included
        }

        [Fact]
        public void WhenGettingDDDAuditLogsWithBifurcateChanges_ThenUnchangedEntitiesWithModifiedChildrenAreIncludedCorrectly()
        {
            var author = new Person() { FirstName = "Saeb", LastName = "Amini" };
            author.Thoughts.Add(new Thought() { Description = "Peaceful" });
            _context.Add(author);
            _context.SaveChanges();
            author.Thoughts.Single().Description = "Ommmmmmmmmm";

            var auditLogs = _auditor.GetLogs<TestAggregateRootBase>(ChangeSnapshotType.Bifurcate);
            auditLogs.Count.ShouldBe(1);
            var log = auditLogs.Single();
            log.ChangeType.ShouldBe(AuditLogChangeType.Modified);
            var changeSnapshot = JsonConvert.DeserializeObject<JObject>(log.ChangeSnapshot);
            var before = changeSnapshot["Before"];
            before.Children<JProperty>().Count().ShouldBe(1);
            before[nameof(Person.Thoughts)][0][nameof(Thought.Id)].ShouldNotBeNull(); // nested entity primary keys should always be included
            before[nameof(Person.Thoughts)][0][nameof(Thought.Description)].Value<string>().ShouldBe("Peaceful");
            var after = changeSnapshot["After"];
            after.Children<JProperty>().Count().ShouldBe(1);
            before[nameof(Person.Thoughts)][0][nameof(Thought.Id)].ShouldNotBeNull(); // nested entity primary keys should always be included
            after[nameof(Person.Thoughts)][0][nameof(Thought.Description)].Value<string>().ShouldBe("Ommmmmmmmmm");
        }

        [Fact]
        public void WhenGettingDDDAuditLogsWithInlineChanges_ThenUnchangedEntitiesWithModifiedChildrenAreIncludedCorrectly()
        {
            var author = new Person() { FirstName = "Saeb", LastName = "Amini" };
            author.Thoughts.Add(new Thought() { Description = "Peaceful" });
            _context.Add(author);
            _context.SaveChanges();
            author.Thoughts.Single().Description = "Ommmmmmmmmm";

            var auditLogs = _auditor.GetLogs<TestAggregateRootBase>(ChangeSnapshotType.Inline);
            auditLogs.Count.ShouldBe(1);
            var log = auditLogs.Single();
            log.ChangeType.ShouldBe(AuditLogChangeType.Modified);
            var changeSnapshot = JsonConvert.DeserializeObject<JObject>(log.ChangeSnapshot);
            changeSnapshot[nameof(Person.Thoughts)][0][nameof(Thought.Id)].ShouldNotBeNull(); // nested entity primary keys should always be included
            changeSnapshot[nameof(Person.Thoughts)][0][nameof(Thought.Description)]["Before"].Value<string>().ShouldBe("Peaceful");
            changeSnapshot[nameof(Person.Thoughts)][0][nameof(Thought.Description)]["After"].Value<string>().ShouldBe("Ommmmmmmmmm");
        }

        [Fact]
        public void WhenGettingSimpleAuditLogsWithBifurcateChanges_ThenParentAndChildEntitiesAreReturnedInSeparateLogItemsWithChangesInSeparateTrees()
        {
            var author = new Person() { FirstName = "Saeb", LastName = "Amini" };
            var thought = new Thought() { Description = "Peaceful" };
            author.Thoughts.Add(thought);
            _context.Add(author);
            _context.SaveChanges();
            author.FirstName = "Kaiser";
            author.Thoughts.Single().Description = "Ommmmmmmmmm";

            var auditLogs = _auditor.GetLogs(ChangeSnapshotType.Bifurcate);
            auditLogs.Count.ShouldBe(2);
            var authorLog = auditLogs.Single(al => al.Entity == author);
            authorLog.ChangeType.ShouldBe(AuditLogChangeType.Modified);
            var authorChangeSnapshot = JsonConvert.DeserializeObject<JObject>(authorLog.ChangeSnapshot);
            var authorBefore = authorChangeSnapshot["Before"];
            authorBefore.Children<JProperty>().Count().ShouldBe(1);
            authorBefore[nameof(Person.FirstName)].Value<string>().ShouldBe("Saeb");
            authorBefore[nameof(Person.LastName)].ShouldBeNull();

            var authorAfter = authorChangeSnapshot["After"];
            authorAfter.Children<JProperty>().Count().ShouldBe(1);
            authorAfter[nameof(Person.FirstName)].Value<string>().ShouldBe("Kaiser");
            authorAfter[nameof(Person.LastName)].ShouldBeNull();

            var thoughtLog = auditLogs.Single(al => al.Entity == thought);
            thoughtLog.ChangeType.ShouldBe(AuditLogChangeType.Modified);
            var thoughtChangeSnapshot = JsonConvert.DeserializeObject<JObject>(thoughtLog.ChangeSnapshot);
            var thoughtBefore = thoughtChangeSnapshot["Before"];
            thoughtBefore.Children<JProperty>().Count().ShouldBe(1);
            thoughtBefore[nameof(Thought.Description)].Value<string>().ShouldBe("Peaceful");

            var thoughtAfter = thoughtChangeSnapshot["After"];
            thoughtAfter.Children<JProperty>().Count().ShouldBe(1);
            thoughtAfter[nameof(Thought.Description)].Value<string>().ShouldBe("Ommmmmmmmmm");
        }

        [Fact]
        public void WhenGettingSimpleAuditLogsWithInlineChanges_ThenParentAndChildEntitiesAreReturnedInSeparateLogItemsWithChangesInSeparateTrees()
        {
            var author = new Person() { FirstName = "Saeb", LastName = "Amini" };
            var thought = new Thought() { Description = "Peaceful" };
            author.Thoughts.Add(thought);
            _context.Add(author);
            _context.SaveChanges();
            author.FirstName = "Kaiser";
            author.Thoughts.Single().Description = "Ommmmmmmmmm";

            var auditLogs = _auditor.GetLogs(ChangeSnapshotType.Inline);
            auditLogs.Count.ShouldBe(2);
            var authorLog = auditLogs.Single(al => al.Entity == author);
            authorLog.ChangeType.ShouldBe(AuditLogChangeType.Modified);
            var authorChangeSnapshot = JsonConvert.DeserializeObject<JObject>(authorLog.ChangeSnapshot);
            authorChangeSnapshot.Children<JProperty>().Count().ShouldBe(1);
            authorChangeSnapshot[nameof(Person.FirstName)]["Before"].Value<string>().ShouldBe("Saeb");
            authorChangeSnapshot[nameof(Person.FirstName)]["After"].Value<string>().ShouldBe("Kaiser");

            var thoughtLog = auditLogs.Single(al => al.Entity == thought);
            thoughtLog.ChangeType.ShouldBe(AuditLogChangeType.Modified);
            var thoughtChangeSnapshot = JsonConvert.DeserializeObject<JObject>(thoughtLog.ChangeSnapshot);
            thoughtChangeSnapshot.Children<JProperty>().Count().ShouldBe(1);
            thoughtChangeSnapshot[nameof(Thought.Description)]["Before"].Value<string>().ShouldBe("Peaceful");
            thoughtChangeSnapshot[nameof(Thought.Description)]["After"].Value<string>().ShouldBe("Ommmmmmmmmm");
        }
    }
}
