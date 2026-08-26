using System.Text.Json;

namespace Murchalka.ModuleSdk.ProcessHost;

internal sealed record GatewayFrame(string Kind, JsonElement Payload);
