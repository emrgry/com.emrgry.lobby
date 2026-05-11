using Cysharp.Threading.Tasks;
using Emrgry.Core;
using Emrgry.Network;
using Emrgry.Network.Connection;
using UnityEngine;

namespace Emrgry.Lobby
{
    /// <summary>
    /// High-level lobby + relay flows. Combines <see cref="ILobbyService"/> with
    /// <see cref="IConnectionService"/> / <see cref="INetworkSceneService"/> so a UI
    /// only has to call one method per user action.
    ///
    /// <para>The flow:</para>
    /// <list type="bullet">
    /// <item><description><b>Host</b> — start relay → create lobby with relay code →
    /// optionally <c>LoadScene(lobbyScene)</c>.</description></item>
    /// <item><description><b>Join</b> — join lobby (by id or code) → read relay join
    /// code from lobby data → join relay.</description></item>
    /// <item><description><b>Leave</b> — leave lobby → disconnect from relay → clear
    /// <see cref="LobbySessionContext"/>.</description></item>
    /// </list>
    ///
    /// <para>All services are resolved from <see cref="ServiceLocator"/> at call time.
    /// Missing services produce a logged failure and a non-success result; callers
    /// are responsible for showing UI feedback.</para>
    /// </summary>
    public static class LobbyConnectionFlow
    {
        /// <summary>
        /// Start a relay host, create a UGS lobby referencing the relay join code,
        /// cache the result in <see cref="LobbySessionContext"/>, and (optionally)
        /// trigger a networked scene load to <paramref name="lobbySceneName"/>.
        /// </summary>
        public static async UniTask<LobbyHostResult> HostGameAsync(
            string lobbyName,
            int maxPlayers,
            bool usePassword,
            string password = null,
            string lobbySceneName = null)
        {
            if (!ServiceLocator.TryResolve<IConnectionService>(out var connection))
                return LobbyHostResult.Failed("IConnectionService not registered.");
            if (!ServiceLocator.TryResolve<ILobbyService>(out var lobby))
                return LobbyHostResult.Failed("ILobbyService not registered.");

            // 1. Relay host.
            var hostResult = await connection.StartHostAsync(new HostConfig(maxPlayers));
            if (!hostResult.Success)
            {
                Debug.LogError($"[LobbyConnectionFlow] StartHostAsync failed: {hostResult.ErrorMessage}");
                return LobbyHostResult.Failed(hostResult.ErrorMessage);
            }

            // 2. Lobby create.
            LobbyInfo lobbyInfo;
            try
            {
                lobbyInfo = await lobby.CreateLobbyAsync(
                    lobbyName,
                    maxPlayers,
                    isPasswordProtected: usePassword,
                    relayJoinCode: hostResult.JoinCode,
                    password: usePassword ? password : null);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyConnectionFlow] CreateLobbyAsync failed: {e.Message}");
                await connection.DisconnectAsync();
                return LobbyHostResult.Failed(e.Message);
            }

            LobbySessionContext.Set(lobbyInfo);

            // 3. Optional networked scene transition.
            if (!string.IsNullOrEmpty(lobbySceneName)
                && ServiceLocator.TryResolve<INetworkSceneService>(out var sceneService))
            {
                sceneService.LoadScene(lobbySceneName);
            }

            return LobbyHostResult.Succeeded(lobbyInfo, hostResult.JoinCode);
        }

        /// <summary>
        /// Join a lobby by UGS id, read its embedded relay code, and join the relay.
        /// </summary>
        public static UniTask<LobbyJoinResult> JoinGameByIdAsync(string lobbyId) =>
            JoinInternal(lobby => lobby.JoinLobbyByIdAsync(lobbyId), $"id={lobbyId}");

        /// <summary>
        /// Join a lobby by short code, read its embedded relay code, and join the relay.
        /// </summary>
        public static UniTask<LobbyJoinResult> JoinGameByCodeAsync(string lobbyCode) =>
            JoinInternal(lobby => lobby.JoinLobbyByCodeAsync(lobbyCode), $"code={lobbyCode}");

        private static async UniTask<LobbyJoinResult> JoinInternal(
            System.Func<ILobbyService, UniTask<LobbyInfo>> joinFn,
            string label)
        {
            if (!ServiceLocator.TryResolve<IConnectionService>(out var connection))
                return LobbyJoinResult.Failed("IConnectionService not registered.");
            if (!ServiceLocator.TryResolve<ILobbyService>(out var lobby))
                return LobbyJoinResult.Failed("ILobbyService not registered.");

            LobbyInfo lobbyInfo;
            try
            {
                lobbyInfo = await joinFn(lobby);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyConnectionFlow] Join failed ({label}): {e.Message}");
                return LobbyJoinResult.Failed(e.Message);
            }

            if (string.IsNullOrEmpty(lobbyInfo.RelayJoinCode))
            {
                var msg = $"Lobby {label} has no RelayJoinCode (host may not have finished starting).";
                Debug.LogError($"[LobbyConnectionFlow] {msg}");
                await lobby.LeaveLobbyAsync();
                return LobbyJoinResult.Failed(msg);
            }

            var joinResult = await connection.JoinAsync(new JoinConfig(lobbyInfo.RelayJoinCode));
            if (!joinResult.Success)
            {
                Debug.LogError($"[LobbyConnectionFlow] Relay join failed: {joinResult.ErrorMessage}");
                await lobby.LeaveLobbyAsync();
                return LobbyJoinResult.Failed(joinResult.ErrorMessage);
            }

            LobbySessionContext.Set(lobbyInfo);
            return LobbyJoinResult.Succeeded(lobbyInfo);
        }

        /// <summary>
        /// Leave the current lobby (or delete it if host), disconnect from the relay,
        /// and clear <see cref="LobbySessionContext"/>. Safe to call at any time.
        /// </summary>
        public static async UniTask LeaveGameAsync(bool deleteIfHost = false)
        {
            if (ServiceLocator.TryResolve<ILobbyService>(out var lobby) && lobby.IsInLobby)
            {
                try
                {
                    if (deleteIfHost) await lobby.DeleteLobbyAsync();
                    else              await lobby.LeaveLobbyAsync();
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[LobbyConnectionFlow] Lobby leave/delete failed: {e.Message}");
                }
            }

            if (ServiceLocator.TryResolve<IConnectionService>(out var connection) && connection.IsConnected)
                await connection.DisconnectAsync();

            LobbySessionContext.Clear();
        }
    }

    public readonly struct LobbyHostResult
    {
        public readonly bool Success;
        public readonly LobbyInfo LobbyInfo;
        public readonly string RelayJoinCode;
        public readonly string ErrorMessage;

        private LobbyHostResult(bool success, LobbyInfo info, string code, string err)
        {
            Success = success;
            LobbyInfo = info;
            RelayJoinCode = code;
            ErrorMessage = err;
        }

        public static LobbyHostResult Succeeded(LobbyInfo info, string code) =>
            new(true, info, code, null);

        public static LobbyHostResult Failed(string err) =>
            new(false, default, null, err);
    }

    public readonly struct LobbyJoinResult
    {
        public readonly bool Success;
        public readonly LobbyInfo LobbyInfo;
        public readonly string ErrorMessage;

        private LobbyJoinResult(bool success, LobbyInfo info, string err)
        {
            Success = success;
            LobbyInfo = info;
            ErrorMessage = err;
        }

        public static LobbyJoinResult Succeeded(LobbyInfo info) => new(true, info, null);
        public static LobbyJoinResult Failed(string err) => new(false, default, err);
    }
}
