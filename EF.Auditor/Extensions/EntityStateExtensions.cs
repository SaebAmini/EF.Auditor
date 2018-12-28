using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace EF.Auditor.Extensions
{
    public static class EntityStateExtensions
    {
        public static bool IsChanged(this EntityState source)
        {
            return new[] { EntityState.Added, EntityState.Deleted, EntityState.Modified }.Contains(source);
        }
    }
}
