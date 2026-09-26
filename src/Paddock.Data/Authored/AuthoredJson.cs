using System.Text.Json;
using System.Text.Json.Serialization;

namespace Paddock.Data.Authored;

internal static class AuthoredJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        AllowTrailingCommas = false,
        NumberHandling = JsonNumberHandling.Strict,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
}
