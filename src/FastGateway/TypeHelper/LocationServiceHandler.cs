using FreeSql.Internal.Model;

namespace FastGateway.TypeHelper;

public sealed class LocationServiceHandler : TypeHandler<List<LocationService>>
{
    public override List<LocationService> Deserialize(object value)
    {
        if(value is string str)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return new();
            }

            return JsonSerializer.Deserialize<List<LocationService>>(str);
        }

        return new List<LocationService>();
    }

    public override object Serialize(List<LocationService> value)
    {
        return JsonSerializer.Serialize(value);
    }
}