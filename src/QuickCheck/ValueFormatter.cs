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
/// <remarks>
/// Exceptions caught while formatting a value produce <c>&lt;Name threw ExceptionType: message&gt;</c>
/// in its place. A record member whose getter throws prints <c>&lt;threw ExceptionType: message&gt;</c>
/// in that member's slot. During expansion, a reference type encountered again within its own
/// expansion prints <c>&lt;cycle&gt;</c>, and nesting past <see cref="MaxDepth"/> prints <c>…</c>.
/// </remarks>
public static class ValueFormatter
{
    private const int MaxCollectionItems = 100;

    /// <summary>
    /// The number of nested tuples, collections, pairs and records a value is expanded through
    /// before the rest prints as <c>…</c>.
    /// </summary>
    public const int MaxDepth = 64;

    /// <summary>
    /// Renders <paramref name="value"/> as it appears in failure reports.
    /// </summary>
    /// <remarks>
    /// The output is for people reading a report, not for parsing: its exact text may change
    /// between versions. Record and <see cref="Memory{T}"/> expansion rely on reflection, so
    /// under trimming they fall back to the value's own <c>ToString</c>.
    /// </remarks>
    public static string Format(object? value)
    {
        var rendering = new Rendering();
        rendering.Append(value);
        return rendering.ToString();
    }

    /// <summary>
    /// One <see cref="Format"/> call: the text so far and the objects currently being expanded,
    /// so a value that contains itself is recognised and a throw inside a value is confined to it.
    /// </summary>
    private sealed class Rendering
    {
        private readonly StringBuilder _builder = new();
        private readonly HashSet<object> _ancestors = new(ReferenceEqualityComparer.Instance);
        private int _depth;

        public override string ToString() => _builder.ToString();

        public void Append(object? value)
        {
            var start = _builder.Length;

            try
            {
                AppendValue(value);
            }
            catch (Exception exception)
            {
                // Whatever the value managed to print before throwing is discarded so the
                // placeholder stands alone rather than completing a half-written expansion.
                _builder.Length = start;
                AppendThrew(value?.GetType().Name, exception);
            }
        }

        private void AppendValue(object? value)
        {
            switch (value)
            {
                case null:
                    _builder.Append("null");
                    break;

                case string text:
                    AppendQuoted(text, '"');
                    break;

                case char character:
                    AppendQuoted(character.ToString(), '\'');
                    break;

                case bool flag:
                    _builder.Append(flag ? "true" : "false");
                    break;

                // The default formats drop the fraction (DateTime) or everything past the minute (TimeOnly),
                // which would print a value one tick after midnight as midnight. The F specifiers omit a
                // zero fraction, so round values stay short.
                case DateTime dateTime:
                    _builder.Append(dateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK", CultureInfo.InvariantCulture));
                    break;

                case DateTimeOffset dateTimeOffset:
                    _builder.Append(dateTimeOffset.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture));
                    break;

                case DateOnly date:
                    _builder.Append(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    break;

                case TimeOnly time:
                    _builder.Append(time.ToString("HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture));
                    break;

                case IFormattable formattable:
                    _builder.Append(formattable.ToString(format: null, CultureInfo.InvariantCulture));
                    break;

                case ITuple tuple:
                    AppendExpanded(tuple, AppendTuple);
                    break;

                case IEnumerable items:
                    AppendExpanded(items, AppendCollection);
                    break;

                case not null when AsArray(value) is { } array:
                    AppendExpanded(value, _ => AppendCollection(array));
                    break;

                case not null when AsKeyValuePair(value) is { } pair:
                    AppendExpanded(value, _ => AppendPair(pair));
                    break;

                case not null when HasSynthesizedToString(value.GetType()):
                    AppendExpanded(value, AppendRecord);
                    break;

                default:
                    _builder.Append(value);
                    break;
            }
        }

        /// <summary>
        /// Runs <paramref name="expand"/> with <paramref name="container"/> registered as an
        /// ancestor of whatever it prints, or prints the cycle or depth placeholder instead.
        /// The ancestor set tracks reference types only; value types, including recurring
        /// boxed structs, rely on the depth cap.
        /// </summary>
        private void AppendExpanded<TContainer>(TContainer container, Action<TContainer> expand)
            where TContainer : notnull
        {
            if (_depth == MaxDepth)
            {
                _builder.Append('…');
                return;
            }

            var isReference = !container.GetType().IsValueType;

            if (isReference && !_ancestors.Add(container))
            {
                _builder.Append("<cycle>");
                return;
            }

            _depth++;

            try
            {
                expand(container);
            }
            finally
            {
                _depth--;

                if (isReference)
                {
                    _ancestors.Remove(container);
                }
            }
        }

        private void AppendTuple(ITuple tuple)
        {
            _builder.Append('(');

            for (var i = 0; i < tuple.Length; i++)
            {
                if (i > 0)
                {
                    _builder.Append(", ");
                }

                Append(tuple[i]);
            }

            _builder.Append(')');
        }

        private void AppendPair((object? Key, object? Value) pair)
        {
            Append(pair.Key);
            _builder.Append(": ");
            Append(pair.Value);
        }

        /// <summary>
        /// Mirrors the record's own <c>Name { Member = value, … }</c> layout, base members first, but
        /// formats each member with this formatter. A member whose getter throws prints the
        /// placeholder in its slot so the record's other members still reach the report.
        /// </summary>
        private void AppendRecord(object record)
        {
            _builder.Append(record.GetType().Name).Append(" {");
            var index = 0;

            foreach (var (name, read) in PrintedMembers(record))
            {
                _builder.Append(index++ == 0 ? " " : ", ").Append(name).Append(" = ");

                object? value;

                try
                {
                    value = read();
                }
                catch (Exception exception)
                {
                    AppendThrew(subject: null, exception);
                    continue;
                }

                Append(value);
            }

            _builder.Append(" }");
        }

        private void AppendCollection(IEnumerable items)
        {
            _builder.Append('[');
            var count = 0;

            foreach (var item in items)
            {
                if (count == MaxCollectionItems)
                {
                    _builder.Append(", …");
                    break;
                }

                if (count > 0)
                {
                    _builder.Append(", ");
                }

                Append(item);
                count++;
            }

            _builder.Append(']');
        }

        private void AppendQuoted(string text, char quote)
        {
            _builder.Append(quote);

            foreach (var character in text)
            {
                switch (character)
                {
                    case '\\': _builder.Append(@"\\"); break;
                    case '\n': _builder.Append(@"\n"); break;
                    case '\r': _builder.Append(@"\r"); break;
                    case '\t': _builder.Append(@"\t"); break;
                    case '\0': _builder.Append(@"\0"); break;
                    case var c when c == quote: _builder.Append('\\').Append(c); break;
                    case var c when char.IsControl(c) || char.IsSurrogate(c):
                        _builder.Append(@"\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                        break;
                    default: _builder.Append(character); break;
                }
            }

            _builder.Append(quote);
        }

        /// <summary>
        /// Prints <c>&lt;subject threw Type: message&gt;</c>, unwrapping the reflection wrapper a
        /// throwing getter arrives in so the report names the exception the value's own code threw.
        /// </summary>
        private void AppendThrew(string? subject, Exception exception)
        {
            var cause = exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;

            _builder.Append('<');

            if (subject is not null)
            {
                _builder.Append(subject).Append(' ');
            }

            _builder.Append("threw ").Append(cause.GetType().Name).Append(": ").Append(SafeMessage(cause)).Append('>');
        }
    }

    internal static string SafeMessage(Exception exception)
    {
        try
        {
            return exception.Message;
        }
        catch (Exception)
        {
            return "<Message threw>";
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
    /// The key and value of a <see cref="KeyValuePair{TKey,TValue}"/> of any type arguments, which
    /// no pattern on <see cref="object"/> can match, or <see langword="null"/> for anything else.
    /// This is what makes a dictionary print as <c>["a": 1, "b": 2]</c>.
    /// </summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2075:MembersOnUnannotatedType",
        Justification = "Key and Value are looked up on a live instance's own type, and a "
            + "trimmed-away lookup yields null, which falls back to the value's ToString.")]
    private static (object? Key, object? Value)? AsKeyValuePair(object value)
    {
        var type = value.GetType();

        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(KeyValuePair<,>))
        {
            return null;
        }

        var key = type.GetProperty(nameof(KeyValuePair<,>.Key));
        var pairValue = type.GetProperty(nameof(KeyValuePair<,>.Value));

        if (key is null || pairValue is null)
        {
            return null;
        }

        return (key.GetValue(value), pairValue.GetValue(value));
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
    /// The members a synthesized <c>ToString</c> prints: every public instance property and
    /// field, base members before derived ones.
    /// </summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2075:MembersOnUnannotatedType",
        Justification = "Best-effort report formatting: a member removed by trimming is merely "
            + "omitted from the report.")]
    private static IEnumerable<(string Name, Func<object?> Read)> PrintedMembers(object record)
    {
        const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
        var type = record.GetType();

        var properties = type.GetProperties(PublicInstance)
            .Where(static property => property.CanRead && property.GetIndexParameters().Length == 0)
            .Select(property => (Depth: InheritanceDepth(property.DeclaringType), property.Name, Read: new Func<object?>(() => property.GetValue(record))));

        var fields = type.GetFields(PublicInstance)
            .Select(field => (Depth: InheritanceDepth(field.DeclaringType), field.Name, Read: new Func<object?>(() => field.GetValue(record))));

        return properties.Concat(fields)
            .OrderBy(static member => member.Depth)
            .Select(static member => (member.Name, member.Read));
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
}
