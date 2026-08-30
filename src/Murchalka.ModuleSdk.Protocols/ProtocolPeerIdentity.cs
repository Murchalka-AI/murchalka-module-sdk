namespace Murchalka.ModuleSdk.Protocols;

/// <summary>Contains the authenticated, non-secret identity of an external protocol peer.</summary>
/// <param name="Subject">The stable authenticated subject.</param>
/// <param name="AuthenticationScheme">The authentication scheme.</param>
/// <param name="CredentialReference">The audit-safe credential reference.</param>
public sealed record ProtocolPeerIdentity(string Subject, string AuthenticationScheme, string CredentialReference);
