using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Emrgry.Lobby
{
    public interface ILobbyService
    {
        string CurrentLobbyId { get; }
        bool IsInLobby { get; }

        /// <param name="isPasswordProtected">If true, a password is required to join; lobby stays public so it appears in browse.</param>
        UniTask<LobbyInfo> CreateLobbyAsync(string lobbyName, int maxPlayers, bool isPasswordProtected, string relayJoinCode, string password = null);
        UniTask<IReadOnlyList<LobbyInfo>> QueryLobbiesAsync();
        UniTask<LobbyInfo> JoinLobbyByIdAsync(string lobbyId);
        UniTask<LobbyInfo> JoinLobbyByCodeAsync(string lobbyCode);
        UniTask LeaveLobbyAsync();
        UniTask UpdateLobbyDataAsync(bool isInGame);
        UniTask DeleteLobbyAsync();

        event Action LobbyChanged;
    }
}
