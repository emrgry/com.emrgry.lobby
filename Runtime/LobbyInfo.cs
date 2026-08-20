namespace Emrgry.Lobby
{
    public struct LobbyInfo
    {
        public string Id;
        public string Name;
        public string LobbyCode;
        public int CurrentPlayers;
        public int MaxPlayers;
        /// <summary>UGS "private" lobby (hidden from browse). Prefer <see cref="IsPasswordProtected"/> for password + listed lobbies.</summary>
        public bool IsPrivate;
        /// <summary>Public lobby data flag: requires password to join (shows lock in browse).</summary>
        public bool IsPasswordProtected;
        public bool IsInGame;
        public string RelayJoinCode;
        public string HostId;
    }
}
