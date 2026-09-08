using System.Text;

namespace QuickCheck;

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
