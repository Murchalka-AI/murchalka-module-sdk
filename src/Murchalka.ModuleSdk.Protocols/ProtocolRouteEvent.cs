using System.Text.Json;

namespace Murchalka.ModuleSdk.Protocols;

/// <summary>Represents one bounded progress, message, completion, or failure event.</summary>
/// <param name="Kind">The event kind.</param>
/// <param name="Sequence">The zero-based monotonically increasing sequence.</param>
/// <param name="Payload">The schema-validated event payload.</param>
public sealed record ProtocolRouteEvent(ProtocolRouteEventKind Kind, long Sequence, JsonElement Payload);
