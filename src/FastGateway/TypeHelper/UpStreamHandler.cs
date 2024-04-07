using FreeSql.Internal.Model;

namespace FastGateway.TypeHelper;

public class UpStreamHandler : TypeHandler<List<UpStream>>
{
    public override List<UpStream> Deserialize(object value)
    {
        if (value is string str)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return new();
            }

            return JsonSerializer.Deserialize<List<UpStream>>(str);
        }

        return new();
    }

    public override object Serialize(List<UpStream> value)
    {
        return JsonSerializer.Serialize(value);
    }
}