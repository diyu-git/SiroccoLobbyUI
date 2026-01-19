using System;
using Steamworks;

namespace SteamLobbyLib;

[Serializable]
public readonly struct LobbyId : IEquatable<LobbyId>
{
    public ulong Value { get; }

    public LobbyId(ulong value)
    {
        Value = value;
    }

    internal CSteamID ToSteamId() => new CSteamID(Value);
    public static LobbyId FromSteamId(CSteamID id) => new LobbyId(id.m_SteamID);

    public override string ToString() => Value.ToString();
    public override int GetHashCode() => Value.GetHashCode();
    public bool Equals(LobbyId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is LobbyId other && Equals(other);

    public static bool operator ==(LobbyId left, LobbyId right) => left.Equals(right);
    public static bool operator !=(LobbyId left, LobbyId right) => !left.Equals(right);

    // Implicit conversions
    public static implicit operator LobbyId(ulong value) => new LobbyId(value);
    public static implicit operator ulong(LobbyId id) => id.Value;
}