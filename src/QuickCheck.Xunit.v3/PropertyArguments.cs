using System.Text;

namespace QuickCheck.Xunit;

/// <summary>
/// One generated set of arguments — for a property method, or for the
/// constructor of a type a generator is derived for — formatted for failure
/// reports as <c>name = value</c> pairs so a counterexample reads like the
/// call that produced it.
/// </summary>
internal sealed class PropertyArguments
{
    private readonly string[] _names;

    public PropertyArguments(string[] names, object?[] values)
    {
        _names = names;
        Values = values;
    }

    public object?[] Values { get; }

    public override string ToString()
    {
        if (Values.Length == 0)
        {
            return "(no arguments)";
        }

        var text = new StringBuilder();

        for (var i = 0; i < Values.Length; i++)
        {
            if (i > 0)
            {
                text.Append(", ");
            }

            text.Append(_names[i]).Append(" = ").Append(ValueFormatter.Format(Values[i]));
        }

        return text.ToString();
    }
}
