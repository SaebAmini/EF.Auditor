using System.Collections.Generic;
using System.Linq;
using EF.Auditor.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Newtonsoft.Json;

namespace EF.Auditor
{
    public static class Audit
    {
        static readonly Dictionary<EntityState, AuditLogChangeType> EntityStateToAuditLogChangeTypeMapping = new Dictionary<EntityState, AuditLogChangeType>()
        {
            { EntityState.Added, AuditLogChangeType.Added },
            { EntityState.Deleted, AuditLogChangeType.Deleted },
            { EntityState.Modified, AuditLogChangeType.Modified },
            { EntityState.Unchanged, AuditLogChangeType.Modified } // consider changed children as "modified"
        };

        /// <summary>
        /// Retrieves audit logs from the provided DbContext with all the changes in an aggregate boundary in one log item.
        /// This must be called before the changetracker changes are discarded e.g. before SaveChanges.
        /// </summary>
        /// <typeparam name="TAggregateRoot">Type of the aggregate root which will be the top-level entry point for gathering audit logs.</typeparam>
        /// <param name="dbContext">The DbContext whose ChangeTracker is used to extract audit logs.</param>
        /// <param name="changeSnapshotType">The desired output type of the change snapshot created from changes.</param>
        /// <param name="changeSnapshotJsonFormatting">The desired change snapshot JSON formatting.</param>
        public static IReadOnlyList<AuditLog> GetLogs<TAggregateRoot>(DbContext dbContext, ChangeSnapshotType changeSnapshotType = ChangeSnapshotType.Inline, Formatting changeSnapshotJsonFormatting = Formatting.None) where TAggregateRoot : class
        {
            var logs = new List<AuditLog>();
            try
            {
                var entries = dbContext.ChangeTracker.Entries<TAggregateRoot>().Where(en => !en.Entity.GetType().IsDefined(typeof(ShouldNotAuditAttribute), false)).ToList();
                // disable change tracking for a significant performance improvement
                // this is safe in this scope as we're only adding new records and don't need change tracking
                dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
                foreach (var entry in entries)
                {
                    bool hasChanged = entry.State.IsChanged();
                    if (!hasChanged && !GetChangedCollectionsAndChildren(dbContext, entry).Any()) continue;
                    string changeSnapshotJson = GetChangeSnapshot(dbContext, entry, changeSnapshotType, changeSnapshotJsonFormatting);
                    logs.Add(new AuditLog
                    (
                        entry.Entity,
                        EntityStateToAuditLogChangeTypeMapping[entry.State],
                        changeSnapshotJson
                    ));
                }
                return logs;
            }
            finally
            {
                dbContext.ChangeTracker.AutoDetectChangesEnabled = true;
            }
        }

        /// <summary>
        /// Retrieves audit logs from the provided DbContext with one log item per changed entity.
        /// This must be called before the changetracker changes are discarded e.g. before SaveChanges.
        /// </summary>
        /// <param name="dbContext">The DbContext whose ChangeTracker is used to extract audit logs.</param>
        /// <param name="changeSnapshotType">The desired output type of the change snapshot created from changes.</param>
        /// <param name="changeSnapshotJsonFormatting">The desired change snapshot JSON formatting.</param>
        public static IReadOnlyList<AuditLog> GetLogs(DbContext dbContext, ChangeSnapshotType changeSnapshotType = ChangeSnapshotType.Inline, Formatting changeSnapshotJsonFormatting = Formatting.None)
        {
            var logs = new List<AuditLog>();
            try
            {
                var entries = dbContext.ChangeTracker.Entries().Where(en => !en.Entity.GetType().IsDefined(typeof(ShouldNotAuditAttribute), false)).ToList();
                // disable change tracking for a significant performance improvement
                // this is safe in this scope as we're only adding new records and don't need change tracking
                dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
                foreach (var entry in entries)
                {
                    bool hasChanged = entry.State.IsChanged();
                    if (!hasChanged) continue;
                    string changeSnapshotJson = GetChangeSnapshot(entry, changeSnapshotType, changeSnapshotJsonFormatting);
                    logs.Add(new AuditLog
                    (
                        entry.Entity,
                        EntityStateToAuditLogChangeTypeMapping[entry.State],
                        changeSnapshotJson
                    ));
                }
                return logs;
            }
            finally
            {
                dbContext.ChangeTracker.AutoDetectChangesEnabled = true;
            }
        }

        private static string GetChangeSnapshot<TAggregateRoot>(DbContext dbContext, EntityEntry<TAggregateRoot> entry, ChangeSnapshotType changeSnapshotType, Formatting jsonFormatting) where TAggregateRoot : class
        {
            switch (changeSnapshotType)
            {
                default:
                case ChangeSnapshotType.Bifurcate:
                    var (Before, After) = GetDeepChangesBifurcate<TAggregateRoot>(dbContext, entry);
                    var deepChangeSnapshot = new { Before, After };
                    return JsonConvert.SerializeObject(deepChangeSnapshot, jsonFormatting);
                case ChangeSnapshotType.Inline:
                    var inlineChanges = GetDeepChangesInline<TAggregateRoot>(dbContext, entry);
                    return JsonConvert.SerializeObject(inlineChanges, jsonFormatting);
            }
        }

        private static string GetChangeSnapshot(EntityEntry entry, ChangeSnapshotType changeSnapshotType, Formatting jsonFormatting)
        {
            switch (changeSnapshotType)
            {
                default:
                case ChangeSnapshotType.Bifurcate:
                    var (Before, After) = GetShallowChangesBifurcate(entry);
                    var deepChangeSnapshot = new { Before, After };
                    return JsonConvert.SerializeObject(deepChangeSnapshot, jsonFormatting);
                case ChangeSnapshotType.Inline:
                    var inlineChanges = GetShallowChangesInline(entry);
                    return JsonConvert.SerializeObject(inlineChanges, jsonFormatting);
            }
        }

        private static Dictionary<string, object> GetDeepChangesInline<TAggregateRoot>(DbContext dbContext, EntityEntry entry) where TAggregateRoot : class
        {
            var changes = new Dictionary<string, object>();
            var props = entry.Properties.Where(p => !p.IsTemporary && (!p.Metadata.IsPrimaryKey() || (p.Metadata.IsPrimaryKey() && !(entry.Entity is TAggregateRoot))));
            switch (entry.State)
            {
                case EntityState.Added:
                    foreach (var prop in props)
                    {
                        changes.Add(prop.Metadata.Name, new InlineChange(null, prop.CurrentValue));
                    }
                    break;
                case EntityState.Deleted:
                    foreach (var prop in props)
                    {
                        changes.Add(prop.Metadata.Name, new InlineChange(prop.CurrentValue, null));
                    }
                    break;
                default:
                    props = props.Where(p => p.IsModified || p.Metadata.IsPrimaryKey());
                    foreach (var prop in props)
                    {
                        changes.Add(prop.Metadata.Name, new InlineChange(prop.OriginalValue, prop.CurrentValue));
                    }
                    break;
            }
            var changedCollectionsAndChildren = GetChangedCollectionsAndChildren(dbContext, entry);
            foreach (var collectionAndChildren in changedCollectionsAndChildren)
            {
                var changesList = new List<Dictionary<string, object>>();
                foreach (var childEntry in collectionAndChildren.Value)
                {
                    var childDeepInlineChanges = GetDeepChangesInline<TAggregateRoot>(dbContext, childEntry);
                    changesList.Add(childDeepInlineChanges);
                }
                if (changesList.Any())
                {
                    changes.Add(collectionAndChildren.Key, changesList);
                }
            }
            return changes;
        }

        private static Dictionary<string, object> GetShallowChangesInline(EntityEntry entry)
        {
            var changes = new Dictionary<string, object>();
            var props = entry.Properties.Where(p => !p.IsTemporary && !p.Metadata.IsPrimaryKey());
            switch (entry.State)
            {
                case EntityState.Added:
                    foreach (var prop in props)
                    {
                        changes.Add(prop.Metadata.Name, new InlineChange(null, prop.CurrentValue));
                    }
                    break;
                case EntityState.Deleted:
                    foreach (var prop in props)
                    {
                        changes.Add(prop.Metadata.Name, new InlineChange(prop.CurrentValue, null));
                    }
                    break;
                default:
                    props = props.Where(p => p.IsModified);
                    foreach (var prop in props)
                    {
                        changes.Add(prop.Metadata.Name, new InlineChange(prop.OriginalValue, prop.CurrentValue));
                    }
                    break;
            }
            return changes;
        }

        private static (Dictionary<string, object> Before, Dictionary<string, object> After) GetDeepChangesBifurcate<TAggregateRoot>(DbContext dbContext, EntityEntry entry) where TAggregateRoot : class
        {
            var beforeParent = new Dictionary<string, object>();
            var afterParent = new Dictionary<string, object>();
            var props = entry.Properties.Where(p => !p.IsTemporary && (!p.Metadata.IsPrimaryKey() || (p.Metadata.IsPrimaryKey() && !(entry.Entity is TAggregateRoot))));
            switch (entry.State)
            {
                case EntityState.Added:
                    foreach (var prop in props)
                    {
                        afterParent.Add(prop.Metadata.Name, prop.CurrentValue);
                    }
                    break;
                case EntityState.Deleted:
                    foreach (var prop in props)
                    {
                        beforeParent.Add(prop.Metadata.Name, prop.CurrentValue);
                    }
                    break;
                default:
                    props = props.Where(p => p.IsModified || p.Metadata.IsPrimaryKey());
                    foreach (var prop in props)
                    {
                        beforeParent.Add(prop.Metadata.Name, prop.OriginalValue);
                        afterParent.Add(prop.Metadata.Name, prop.CurrentValue);
                    }
                    break;
            }
            var changedCollectionsAndChildren = GetChangedCollectionsAndChildren(dbContext, entry);
            foreach (var collectionAndChildren in changedCollectionsAndChildren)
            {
                var beforeList = new List<Dictionary<string, object>>();
                var afterList = new List<Dictionary<string, object>>();
                foreach (var childEntry in collectionAndChildren.Value)
                {
                    var (Before, After) = GetDeepChangesBifurcate<TAggregateRoot>(dbContext, childEntry);
                    if (Before.Any())
                    {
                        beforeList.Add(Before);
                    }
                    if (After.Any())
                    {
                        afterList.Add(After);
                    }
                }
                if (beforeList.Any())
                {
                    beforeParent.Add(collectionAndChildren.Key, beforeList);
                }
                if (afterList.Any())
                {
                    afterParent.Add(collectionAndChildren.Key, afterList);
                }
            }
            return (beforeParent, afterParent);
        }

        private static (Dictionary<string, object> Before, Dictionary<string, object> After) GetShallowChangesBifurcate(EntityEntry entry)
        {
            var beforeParent = new Dictionary<string, object>();
            var afterParent = new Dictionary<string, object>();
            var props = entry.Properties.Where(p => !p.IsTemporary && !p.Metadata.IsPrimaryKey());
            switch (entry.State)
            {
                case EntityState.Added:
                    foreach (var prop in props)
                    {
                        afterParent.Add(prop.Metadata.Name, prop.CurrentValue);
                    }
                    break;
                case EntityState.Deleted:
                    foreach (var prop in props)
                    {
                        beforeParent.Add(prop.Metadata.Name, prop.CurrentValue);
                    }
                    break;
                default:
                    props = props.Where(p => p.IsModified);
                    foreach (var prop in props)
                    {
                        beforeParent.Add(prop.Metadata.Name, prop.OriginalValue);
                        afterParent.Add(prop.Metadata.Name, prop.CurrentValue);
                    }
                    break;
            }
            return (beforeParent, afterParent);
        }

        private static IEnumerable<KeyValuePair<string, IEnumerable<EntityEntry>>> GetChangedCollectionsAndChildren(DbContext dbContext, EntityEntry entry)
        {
            var entryPk = entry.Properties.First(p => p.Metadata.IsPrimaryKey()).CurrentValue;
            foreach (var collection in entry.Collections.Where(c => c.CurrentValue != null))
            {
                var collectionEntityType = collection.CurrentValue.GetType().GetGenericArguments().Single();
                var changedEntityEntries = dbContext.ChangeTracker
                    .Entries()
                    .Where(en => en.Metadata.ClrType == collectionEntityType &&
                        en.Properties.Any(p => p.Metadata.IsForeignKey() && ((p.OriginalValue?.Equals(entryPk) ?? false) || (p.CurrentValue?.Equals(entryPk) ?? false))) &&
                        (en.State.IsChanged() || GetChangedCollectionsAndChildren(dbContext, en).Any()));
                if (changedEntityEntries.Any())
                {
                    yield return new KeyValuePair<string, IEnumerable<EntityEntry>>(collection.Metadata.Name, changedEntityEntries.ToList());
                }
            }
        }
    }
}
