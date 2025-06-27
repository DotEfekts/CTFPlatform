using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json;

namespace CTFPlatform.Utilities;

public class X509Certificate2JsonConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(X509Certificate2);
    }

    public override object? ReadJson(JsonReader reader,
        Type objectType, object? existingValue, JsonSerializer serializer)
    {
        try
        {
            var deserializedRaw = serializer.Deserialize<byte[]>(reader);
            if (deserializedRaw == null)
                throw new JsonSerializationException("Failed to deserialize certificate.");

            var deserialized = X509CertificateLoader.LoadPkcs12(deserializedRaw, null,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet, Pkcs12LoaderLimits.Defaults);
            return deserialized;
        }
        catch
        {
            return null;
        }
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value == null)
            return;
        
        var certData = ((X509Certificate2)value).Export(X509ContentType.Pkcs12);
        serializer.Serialize(writer, certData);
    }
}