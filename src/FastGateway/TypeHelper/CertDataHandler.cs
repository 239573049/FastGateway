using FreeSql.Internal.Model;

namespace FastGateway.TypeHelper;

public sealed class CertDataHandler : TypeHandler<List<CertData>>
{
    public override List<CertData> Deserialize(object value)
    {
        if (value is string str)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return new();
            }
            return JsonSerializer.Deserialize<List<CertData>>(str);
        }

        return new();
    }

    public override object Serialize(List<CertData> value)
    {
        return JsonSerializer.Serialize(value);
    }
}