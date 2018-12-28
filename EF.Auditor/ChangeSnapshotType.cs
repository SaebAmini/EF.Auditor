namespace EF.Auditor
{
    public enum ChangeSnapshotType
    {
        /// <summary>
        /// Specifies a type of change snapshot where before and after values are in separate top-level "Before" and "After" matching trees.
        /// </summary>
        Bifurcate,
        /// <summary>
        /// Specifies a type of change snapshot where before and after values are inline each property in "Before" and "After" keys.
        /// </summary>
        Inline
    }
}
