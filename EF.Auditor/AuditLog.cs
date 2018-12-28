namespace EF.Auditor
{
    public class AuditLog
    {
        public object Entity { get; private set; }
        public AuditLogChangeType ChangeType { get; private set; }
        public string ChangeSnapshot { get; private set; }

        private AuditLog() { }

        internal AuditLog(object entity, AuditLogChangeType changeType, string changeSnapshot)
        {
            Entity = entity;
            ChangeType = changeType;
            ChangeSnapshot = changeSnapshot;
        }
    }
}
