using System.Collections.Generic;
namespace Martin.Runtime;

public readonly struct Optional<T>
{
    private readonly T _value;

    public bool HasValue { get; }

    public T Value
    {
        get {
            if (!HasValue)
                throw new InvalidOperationException("Optional has no value.");

            return _value;
        }
    }

    public Optional(T value)
    {
        _value = value;
        HasValue = true;
    }

    public static Optional<T> None => default;

    public static bool operator ==(Optional<T> left, Optional<T> right) =>
        left.HasValue == right.HasValue && (!left.HasValue || EqualityComparer<T>.Default.Equals(left._value, right._value));

    public static bool operator !=(Optional<T> left, Optional<T> right) => !(left == right);

    public override bool Equals(object? obj) => obj is Optional<T> other && this == other;

    public override int GetHashCode() => HasValue ? EqualityComparer<T>.Default.GetHashCode(_value!) : 0;
}
