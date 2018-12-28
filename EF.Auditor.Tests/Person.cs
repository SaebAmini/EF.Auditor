using System.Collections.Generic;

namespace EF.Auditor.Tests
{
    internal class Person : TestAggregateRootBase
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public List<Thought> Thoughts { get; set; } = new List<Thought>();

        public Person()
        { }
    }
}