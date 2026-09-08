using System.Text;

namespace QuickCheck;

/// <summary>
/// The <c>ToString</c> of a command sequence, whatever its command type: one command per line,
/// as a failure report prints them.
/// </summary>
internal static class CommandSequenceFormat
{
    public static string Format<TCommand>(IReadOnlyList<TCommand> commands)
    {
        if (commands.Count == 0)
        {
            return "[]";
        }

        var builder = new StringBuilder();

        for (var i = 0; i < commands.Count; i++)
        {
            if (i > 0)
            {
                builder.AppendLine();
            }

            builder.Append(ValueFormatter.Format(commands[i]));
        }

        return builder.ToString();
    }
}
