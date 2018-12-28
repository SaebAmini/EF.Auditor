namespace EF.Auditor
{
    internal class InlineChange
    {
        public object Before { get; }
        public object After { get; }

        public InlineChange(object before, object after)
        {
            Before = before;
            After = after;
        }
    }
}
