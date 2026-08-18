using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace QuickCheck;

/// <summary>
/// Renders example values for failure reports.
/// </summary>
internal static class ValueFormatter
{
    private const int MaxCollectionItems = 100;

    public static string Format(object? value)
    {
        var builder = new StringBuilder();
        Append(builder, value);
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, object? value)
    {
        switch (value)
        {
            case null:
                builder.Append("null");
                break;

            case string text:
                AppendQuoted(builder, text, '"');
                break;

            case char character:
                AppendQuoted(builder, character.ToString(), '\'');
                break;

            case bool flag:
                builder.Append(flag ? "true" : "false");
                break;

            case IFormattable formattable:
                builder.Append(formattable.ToString(format: null, CultureInfo.InvariantCulture));
                break;

            case ITuple tuple:
                builder.Append('(');

                for (var i = 0; i < tuple.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }

                    Append(builder, tuple[i]);
                }

                builder.Append(')');
                break;

            case IEnumerable items:
                AppendCollection(builder, items);
                break;

            case not null when AsArray(value) is { } array:
                AppendCollection(builder, array);
                break;

            default:
                builder.Append(value);
                break;
        }
    }

    /// <summary>
    /// The contents of a <see cref="Memory{T}"/> or <see cref="ReadOnlyMemory{T}"/>, which reach
    /// their elements only through their own generic API, or <see langword="null"/> for anything
    /// else.
    /// </summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2075:MembersOnUnannotatedType",
        Justification = "ToArray is looked up on a live instance's own type, and a trimmed-away "
            + "lookup yields null, which falls back to the value's ToString.")]
    private static Array? AsArray(object value)
    {
        var type = value.GetType();

        if (!type.IsGenericType)
        {
            return null;
        }

        var definition = type.GetGenericTypeDefinition();

        if (definition != typeof(Memory<>) && definition != typeof(ReadOnlyMemory<>))
        {
            return null;
        }

        return (Array?)type.GetMethod(nameof(Memory<byte>.ToArray), Type.EmptyTypes)?.Invoke(value, null);
    }

    private static void AppendCollection(StringBuilder builder, IEnumerable items)
    {
        builder.Append('[');
        var count = 0;

        foreach (var item in items)
        {
            if (count == MaxCollectionItems)
            {
                builder.Append(", …");
                break;
            }

            if (count > 0)
            {
                builder.Append(", ");
            }

            Append(builder, item);
            count++;
        }

        builder.Append(']');
    }

    private static void AppendQuoted(StringBuilder builder, string text, char quote)
    {
        builder.Append(quote);

        foreach (var character in text)
        {
            switch (character)
            {
                case '\\': builder.Append(@"\\"); break;
                case '\n': builder.Append(@"\n"); break;
                case '\r': builder.Append(@"\r"); break;
                case '\t': builder.Append(@"\t"); break;
                case '\0': builder.Append(@"\0"); break;
                case var c when c == quote: builder.Append('\\').Append(c); break;
                case var c when char.IsControl(c) || char.IsSurrogate(c):
                    builder.Append(@"\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    break;
                default: builder.Append(character); break;
            }
        }

        builder.Append(quote);
    }
}
