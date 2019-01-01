[![NuGet version (EF.Auditor)](https://img.shields.io/nuget/v/EF.Auditor.svg?style=flat)](https://www.nuget.org/packages/EF.Auditor)
[![Build Status](https://dev.azure.com/saebamini/Personal/_apis/build/status/EF.Auditor?branchName=master)](https://dev.azure.com/saebamini/Personal/_build/latest?definitionId=1?branchName=master)


EF.Auditor is a simple and lightweight library that piggybacks on top of **Entity Framework Core**'s internal ChangeTracker to give you auditing information about added, deleted and modified entities.

## Installing via NuGet

```
Install-Package EF.Auditor
```

## Getting Started

The main type you want is `Audit`. In your unit of work, when you're done with making changes in your DbContext (i.e. adding/deleting/modifying entities or their collections), this is how you get the auditing information:

```csharp
// get one audit log per changed entity
var auditLogs = Audit.GetLogs(dbContext);

// or

// get one audit log per DDD aggregate boundary
var auditLogs = Audit.GetLogs<AggregateRootBase>(dbContext);
```

Note that this should be done _before_ `SaveChanges` or anything else that resets EF's change tracker.

With the non-generic overload, you simply get one audit log per changed entity.

In the generic overload, `AggregateRootBase` is the top-level entry point for gathering audit logs in a DDD sense, so you get all deep changes within a DDD aggregate boundary that happened in your unit of work in one log.

`AuditLog` looks like this:

```csharp
// The entity this audit log belongs to.
public object Entity { get; private set; }
// The type of change this audit log holds, e.g. Added, Deleted or Modified
public AuditLogChangeType ChangeType { get; private set; }
// Audit log information in JSON format
public string ChangeSnapshot { get; private set; }
```

You can then use these audit logs how you want, such as persisting them to the storage of your choice.

### Wait, a static class/method? what about dependency injection? How do I mock it?!

Chillax. There's an `Auditor` class that implements the `IAuditor` interface:

```csharp
public interface IAuditor
{
    IReadOnlyList<AuditLog> GetLogs<TAggregateRoot>(ChangeSnapshotType changeSnapshotType = ChangeSnapshotType.Inline, Formatting changeSnapshotJsonFormatting = Formatting.None) where TAggregateRoot : class;

    IReadOnlyList<AuditLog> GetLogs(ChangeSnapshotType changeSnapshotType = ChangeSnapshotType.Inline, Formatting changeSnapshotJsonFormatting = Formatting.None);
}
```

And you can instantiate it with a `DbContext` as a dependency:

```csharp
var auditor = new Auditor(dbContext);
```

If you prefer to use it like that, it's best to set this up with your favourite IoC container and configure it to have the same lifetime as your DbContext.


## ChangeSnapshot Type

There are two formats in which the `ChangeSnapshot` can be generated: `Inline` (default) and `Bifurcate` which can be specified as an argument when calling `GetLogs`:

```csharp
var auditLogs = Audit.GetLogs<AggregateRootBase>(dbContext, ChangeSnapshotType.Inline)

// or

var auditLogs = Audit.GetLogs<AggregateRootBase>(dbContext, ChangeSnapshotType.Bifurcate)
```

The difference is that with `Bifurcate`, before and after entity property values are in separate top-level "Before" and "After" trees which follow the same schema as the entity, whereas with `Inline`, each entity property will have inline "Before" and "After" keys which hold respective values for the property.

To get a better idea of how these look like, refer to the below samples.


### ChangeSnapshot samples

Considering these sample types:

```csharp
public class AggregateRootBase
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; private set; }
}

public class Person : AggregateRootBase
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public List<Thought> Thoughts { get; set; } = new List<Thought>();

    public Person()
    { }
}

public class Thought
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; private set; }
    public string Description { get; set; }

    public Thought()
    { }
}
```

Here are a few samples of how ChangeSnapshot can look like:

```csharp
var author = new Person() { FirstName = "Saeb", LastName = "Amini" };
dbContext.Add(author);
dbContext.SaveChanges();
author.FirstName = "Kaiser";

var auditLogsBifurcate = Audit.GetLogs<AggregateRootBase>(dbContext, ChangeSnapshotType.Bifurcate);

Console.WriteLine(auditLogsBifurcate.Single().ChangeSnapshot);

/*
{
  "Before": {
    "FirstName": "Saeb"
  },
  "After": {
    "FirstName": "Kaiser"
  }
}
*/

var auditLogsInline = Audit.GetLogs<AggregateRootBase>(dbContext, ChangeSnapshotType.Inline);

Console.WriteLine(auditLogsInline.Single().ChangeSnapshot);

/*
{
  "FirstName": {
    "Before": "Saeb",
    "After": "Kaiser"
  }
}
*/
```

```csharp
var author = new Person() { FirstName = "Saeb", LastName = "Amini" };
author.Thoughts.Add(new Thought() { Description = "Peaceful" });
dbContext.Add(author);
dbContext.SaveChanges();
author.Thoughts.Single().Description = "Ommmmmmmmmm";

var auditLogsBifurcate = Audit.GetLogs<AggregateRootBase>(dbContext, ChangeSnapshotType.Bifurcate);

Console.WriteLine(auditLogsBifurcate.Single().ChangeSnapshot);

/*
{
  "Before": {
    "Thoughts": [
      {
        "Id": 1,
        "Description": "Peaceful"
      }
    ]
  },
  "After": {
    "Thoughts": [
      {
        "Id": 1,
        "Description": "Ommmmmmmmmm"
      }
    ]
  }
}
*/

var auditLogsInline = Audit.GetLogs<AggregateRootBase>(dbContext, ChangeSnapshotType.Inline);

Console.WriteLine(auditLogsInline.Single().ChangeSnapshot);

/*
{
  "Thoughts": [
    {
      "Id": {
        "Before": 1,
        "After": 1
      },
      "Description": {
        "Before": "Peaceful",
        "After": "Ommmmmmmmmm"
      }
    }
  ]
}
*/
```

Note that primary keys for non-top-level entities are always included so when multiple nested children are modified, the exact entity that each bit of the JSON is referring to can be determined.