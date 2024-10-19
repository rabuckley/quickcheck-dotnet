namespace QuickCheck;

public delegate TOutput QuickCheckFunction<in TInput, out TOutput>(TInput input);

public delegate bool QuickCheckConstraint<in TInput>(TInput input);
