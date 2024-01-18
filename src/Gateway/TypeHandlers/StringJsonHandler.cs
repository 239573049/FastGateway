using System.Text.Json;
using FreeSql.Internal.Model;

namespace Gateway.TypeHandlers;

public class StringJsonHandler : TypeHandler<Dictionary<string,string>>
{
    public override Dictionary<string, string> Deserialize(object value)
    {
        return JsonSerializer.Deserialize<Dictionary<string,string>>((string)value);
    }

    public override object Serialize(Dictionary<string, string> value)
    {
        return JsonSerializer.Serialize(value);
    }
}