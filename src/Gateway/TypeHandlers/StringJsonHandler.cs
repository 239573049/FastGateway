namespace Gateway.TypeHandlers;

public class StringJsonHandler<T> : TypeHandler<T>
{
    public override T Deserialize(object value)
    {
        return JsonSerializer.Deserialize<T>((string)value);
    }

    public override object Serialize(T value)
    {
        return JsonSerializer.Serialize(value);
    }
}