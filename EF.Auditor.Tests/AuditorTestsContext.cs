using Microsoft.EntityFrameworkCore;

namespace EF.Auditor.Tests
{
    internal class AuditorTestsContext : DbContext
    {
        public AuditorTestsContext() : base(GetTestDbOptions())
        {
            base.Database.EnsureCreated();
        }

        private static DbContextOptions<AuditorTestsContext> GetTestDbOptions()
        {
            var optionsBuilder = new DbContextOptionsBuilder<AuditorTestsContext>();
            optionsBuilder.UseInMemoryDatabase("AuditorTests");
            return optionsBuilder.Options;
        }

        public DbSet<Person> Persons { get; set; }

        public override void Dispose()
        {
            base.Database.EnsureDeleted();
            base.Dispose();
        }
    }
}
