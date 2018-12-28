using System;

namespace EF.Auditor
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ShouldNotAuditAttribute : Attribute
    {
    }
}
