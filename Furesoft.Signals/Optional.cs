namespace Furesoft.Signals
{
    public class Optional<T>
    {
        public Optional(T value)
        {
            if (value != null)
            {
                HasValue = true;
                Value = value;
            }
        }

        public Optional()
        {
        }

        public T Value { get; }
        public bool HasValue { get; }

        public static Optional<TValue> Some<TValue>(TValue value)
        {
            return new Optional<TValue>(value);
        }

        public static Optional<T> None => new Optional<T>();

        public static implicit operator bool(Optional<T> opt)
        {
            return opt.HasValue;
        }

        public static implicit operator T(Optional<T> opt)
        {
            if (opt.HasValue)
            {
                return opt.Value;
            }

            return default(T);
        }

        public static implicit operator Optional<T>(bool value) => Optional<T>.None;

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public static class ObjectExtentions
    {
        public static Optional<T> ToOptional<T>(this T target)
        {
            return Optional<T>.Some(target);
        }
    }
}