using FreeSql.Internal.Model;

namespace FastGateway.TypeHelper;

public sealed class StringHandler : TypeHandler<string[]>
{
    public override string[] Deserialize(object value)
    {
        return ((string)value).Split(',');
    }

    public override object Serialize(string[] value)
    {
        return string.Join(',', value);
    }
}