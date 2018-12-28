using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace EF.Auditor
{
    public interface IAuditor
    {
        IReadOnlyList<AuditLog> GetLogs<TAggregateRoot>(ChangeSnapshotType changeSnapshotType = ChangeSnapshotType.Inline, Formatting changeSnapshotJsonFormatting = Formatting.None) where TAggregateRoot : class;
        IReadOnlyList<AuditLog> GetLogs(ChangeSnapshotType changeSnapshotType = ChangeSnapshotType.Inline, Formatting changeSnapshotJsonFormatting = Formatting.None);
    }

    public class Auditor : IAuditor
    {
        readonly DbContext _context;

        public Auditor(DbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves audit logs from the underlying DbContext with all the changes in an aggregate boundary in one log item.
        /// This must be called before the changetracker changes are discarded e.g. before SaveChanges.
        /// </summary>
        /// <typeparam name="TAggregateRoot">Type of the aggregate root which will be the top-level entry point for gathering audit logs.</typeparam>
        /// <param name="changeSnapshotType">The desired output type of the change snapshot created from changes.</param>
        /// <param name="changeSnapshotJsonFormatting">The desired change snapshot JSON formatting.</param>
        public IReadOnlyList<AuditLog> GetLogs<TAggregateRoot>(ChangeSnapshotType changeSnapshotType = ChangeSnapshotType.Inline, Formatting changeSnapshotJsonFormatting = Formatting.None) where TAggregateRoot : class
        {
            return Audit.GetLogs<TAggregateRoot>(_context, changeSnapshotType, changeSnapshotJsonFormatting);
        }

        /// <summary>
        /// Retrieves audit logs from the provided DbContext with one log item per changed entity.
        /// This must be called before the changetracker changes are discarded e.g. before SaveChanges.
        /// </summary>
        /// <param name="changeSnapshotType">The desired output type of the change snapshot created from changes.</param>
        /// <param name="changeSnapshotJsonFormatting">The desired change snapshot JSON formatting.</param>
        public IReadOnlyList<AuditLog> GetLogs(ChangeSnapshotType changeSnapshotType = ChangeSnapshotType.Inline, Formatting changeSnapshotJsonFormatting = Formatting.None)
        {
            return Audit.GetLogs(_context, changeSnapshotType, changeSnapshotJsonFormatting);
        }
    }
}
