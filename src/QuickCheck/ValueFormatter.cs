using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace QuickCheck;

/// <summary>
/// Renders example values for failure reports: quotes strings and chars, expands tuples,
/// collections and records, and prints <see langword="null"/> explicitly, because the default
/// <c>ToString</c> of such a value is either uninformative or does not format what it contains.
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

            case not null when HasSynthesizedToString(value.GetType()):
                AppendRecord(builder, value);
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

    /// <summary>
    /// Whether <paramref name="type"/> is a record, class or struct, whose <c>ToString</c> the
    /// compiler generated; a hand-written override is honoured instead, because a type that has
    /// defined how it prints means it.
    /// </summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070:UnrecognizedReflectionPattern",
        Justification = "A ToString override is not trimmed while its type is reachable, and a "
            + "missing member only disables record expansion.")]
    private static bool HasSynthesizedToString(Type type)
    {
        var toString = type.GetMethod(nameof(ToString), BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        return toString?.DeclaringType == type
            && toString.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);
    }

    /// <summary>
    /// Mirrors the record's own <c>Name { Member = value, … }</c> layout, base members first, but
    /// formats each member with this formatter.
    /// </summary>
    private static void AppendRecord(StringBuilder builder, object record)
    {
        builder.Append(record.GetType().Name).Append(" {");
        var index = 0;

        foreach (var (name, value) in PrintedMembers(record))
        {
            builder.Append(index++ == 0 ? " " : ", ").Append(name).Append(" = ");
            Append(builder, value);
        }

        builder.Append(" }");
    }

    /// <summary>
    /// The members a synthesized <c>ToString</c> prints: every public instance property and
    /// field, base members before derived ones.
    /// </summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2075:MembersOnUnannotatedType",
        Justification = "Best-effort report formatting: a member removed by trimming is merely "
            + "omitted from the report.")]
    private static IEnumerable<(string Name, object? Value)> PrintedMembers(object record)
    {
        const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
        var type = record.GetType();

        var properties = type.GetProperties(PublicInstance)
            .Where(static property => property.CanRead && property.GetIndexParameters().Length == 0)
            .Select(property => (Depth: InheritanceDepth(property.DeclaringType), property.Name, Value: property.GetValue(record)));

        var fields = type.GetFields(PublicInstance)
            .Select(field => (Depth: InheritanceDepth(field.DeclaringType), field.Name, Value: field.GetValue(record)));

        return properties.Concat(fields)
            .OrderBy(static member => member.Depth)
            .Select(static member => (member.Name, member.Value));
    }

    private static int InheritanceDepth(Type? type)
    {
        var depth = 0;

        for (var current = type; current is not null; current = current.BaseType)
        {
            depth++;
        }

        return depth;
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
