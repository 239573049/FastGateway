using FreeSql.Internal.Model;

namespace FastGateway.TypeHelper;

public sealed class StringJsonHandler<T> : TypeHandler<T>
{
    public override T Deserialize(object value)
    {
        if (value is null)
        {
            return default;
        }

        if (value is string str)
        {
            if(string.IsNullOrWhiteSpace(str))
            {
                return default;
            }
            return JsonSerializer.Deserialize<T>(str);
        }
        return default;
    }

    public override object Serialize(T value)
    {
        if (value is null)
        {
            return null;
        }
        return JsonSerializer.Serialize(value);
    }
}