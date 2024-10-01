namespace QuickCheck.Generators;

public sealed class ArbitraryTupleGenerator<T1, T2> : ArbitraryValueGenerator<(T1, T2)>
{
    private readonly IArbitraryValueGenerator<T1> _t1ValueGenerator;
    private readonly IArbitraryValueGenerator<T2> _t2ValueGenerator;

    public ArbitraryTupleGenerator(
        IArbitraryValueGenerator<T1> t1ValueGenerator,
        IArbitraryValueGenerator<T2> t2ValueGenerator,
        Random random) : base(random)
    {
        _t1ValueGenerator = t1ValueGenerator;
        _t2ValueGenerator = t2ValueGenerator;
    }

    public override (T1, T2) Generate()
    {
        return (_t1ValueGenerator.Generate(), _t2ValueGenerator.Generate());
    }

    public override IEnumerable<(T1, T2)> Shrink((T1, T2) from)
    {
        var (t1, t2) = from;

        var shrunk = _t1ValueGenerator.Shrink(t1).Select(t => (t, t2));
        var shrunk2 = _t2ValueGenerator.Shrink(t2).Select(t => (t1, t));

        return shrunk.Concat(shrunk2);
    }
}

public sealed class ArbitraryTupleGenerator<T1, T2, T3> : ArbitraryValueGenerator<(T1, T2, T3)>
{
    private readonly IArbitraryValueGenerator<T1> _t1ValueGenerator;
    private readonly IArbitraryValueGenerator<T2> _t2ValueGenerator;
    private readonly IArbitraryValueGenerator<T3> _t3ValueGenerator;

    public ArbitraryTupleGenerator(
        IArbitraryValueGenerator<T1> t1ValueGenerator,
        IArbitraryValueGenerator<T2> t2ValueGenerator,
        IArbitraryValueGenerator<T3> t3ValueGenerator,
        Random random) : base(random)
    {
        _t1ValueGenerator = t1ValueGenerator;
        _t2ValueGenerator = t2ValueGenerator;
        _t3ValueGenerator = t3ValueGenerator;
    }

    public override (T1, T2, T3) Generate()
    {
        return (_t1ValueGenerator.Generate(), _t2ValueGenerator.Generate(), _t3ValueGenerator.Generate());
    }

    public override IEnumerable<(T1, T2, T3)> Shrink((T1, T2, T3) from)
    {
        var (t1, t2, t3) = from;

        var shrunk = _t1ValueGenerator.Shrink(t1).Select((t) => (t, t2, t3));
        var shrunk2 = _t2ValueGenerator.Shrink(t2).Select(t => (t1, t, t3));
        var shrunk3 = _t3ValueGenerator.Shrink(t3).Select(t => (t1, t2, t));

        return shrunk.Concat(shrunk2).Concat(shrunk3);
    }
}