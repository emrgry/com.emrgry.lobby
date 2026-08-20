using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UGSLobbyService = Unity.Services.Lobbies.LobbyService;

namespace Emrgry.Lobby
{
    public sealed class LobbyService : ILobbyService, IDisposable
    {
        private const float HeartbeatIntervalSeconds = 15f;
        private const string KeyRelayJoinCode = "relayJoinCode";
        private const string KeyIsInGame = "isInGame";
        private const string KeyHasPassword = "hasPassword";

        private Unity.Services.Lobbies.Models.Lobby _currentLobby;
        private CancellationTokenSource _heartbeatCts;

        public string CurrentLobbyId => _currentLobby?.Id;
        public bool IsInLobby => _currentLobby != null;

        public event Action LobbyChanged;

        public async UniTask<LobbyInfo> CreateLobbyAsync(
            string lobbyName, int maxPlayers, bool isPasswordProtected, string relayJoinCode, string password = null)
        {
            var data = new Dictionary<string, DataObject>
            {
                [KeyRelayJoinCode] = new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode),
                [KeyIsInGame] = new DataObject(DataObject.VisibilityOptions.Public, "false")
            };

            if (isPasswordProtected)
            {
                if (string.IsNullOrEmpty(password))
                    throw new ArgumentException("Password required when lobby is password protected.", nameof(password));
                data[KeyHasPassword] = new DataObject(DataObject.VisibilityOptions.Public, "true");
            }

            // Password-protected lobbies stay public so they appear in QueryLobbies; UGS enforces the password server-side.
            var options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Player = MakePlayer(),
                Data = data,
                Password = isPasswordProtected ? NormalizePassword(password) : null
            };

            _currentLobby = await UGSLobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            Debug.Log($"[LobbyService] Created lobby '{lobbyName}' (id={_currentLobby.Id}, passwordProtected={isPasswordProtected})");

            StartHeartbeat();
            LobbyChanged?.Invoke();

            return ToLobbyInfo(_currentLobby);
        }

        public async UniTask<IReadOnlyList<LobbyInfo>> QueryLobbiesAsync()
        {
            var options = new QueryLobbiesOptions
            {
                Count = 25,
                Filters = new List<QueryFilter>
                {
                    new(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                },
                Order = new List<QueryOrder>
                {
                    new(false, QueryOrder.FieldOptions.Created)
                }
            };

            var response = await UGSLobbyService.Instance.QueryLobbiesAsync(options);
            var results = new List<LobbyInfo>(response.Results.Count);

            foreach (var lobby in response.Results)
                results.Add(ToLobbyInfo(lobby));

            Debug.Log($"[LobbyService] Query returned {results.Count} lobbies.");
            return results;
        }

        public async UniTask<LobbyInfo> JoinLobbyByIdAsync(string lobbyId, string password = null)
        {
            var options = new JoinLobbyByIdOptions
            {
                Player = MakePlayer(),
                Password = string.IsNullOrEmpty(password) ? null : NormalizePassword(password)
            };

            try
            {
                _currentLobby = await UGSLobbyService.Instance.JoinLobbyByIdAsync(lobbyId, options);
            }
            catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.IncorrectPassword)
            {
                throw new LobbyPasswordException(e);
            }

            Debug.Log($"[LobbyService] Joined lobby '{_currentLobby.Name}' (id={lobbyId})");

            LobbyChanged?.Invoke();
            return ToLobbyInfo(_currentLobby);
        }

        public async UniTask<LobbyInfo> JoinLobbyByCodeAsync(string lobbyCode, string password = null)
        {
            var options = new JoinLobbyByCodeOptions
            {
                Player = MakePlayer(),
                Password = string.IsNullOrEmpty(password) ? null : NormalizePassword(password)
            };

            try
            {
                _currentLobby = await UGSLobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, options);
            }
            catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.IncorrectPassword)
            {
                throw new LobbyPasswordException(e);
            }

            Debug.Log($"[LobbyService] Joined lobby by code '{lobbyCode}'");

            LobbyChanged?.Invoke();
            return ToLobbyInfo(_currentLobby);
        }

        public async UniTask LeaveLobbyAsync()
        {
            if (_currentLobby == null) return;

            StopHeartbeat();
            var lobbyId = _currentLobby.Id;
            var playerId = AuthenticationService.Instance.PlayerId;

            try
            {
                await UGSLobbyService.Instance.RemovePlayerAsync(lobbyId, playerId);
                Debug.Log($"[LobbyService] Left lobby {lobbyId}");
            }
            catch (LobbyServiceException e)
            {
                Debug.LogWarning($"[LobbyService] Failed to leave lobby: {e.Message}");
            }

            _currentLobby = null;
            LobbyChanged?.Invoke();
        }

        public async UniTask UpdateLobbyDataAsync(bool isInGame)
        {
            if (_currentLobby == null) return;

            var options = new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    [KeyIsInGame] = new DataObject(DataObject.VisibilityOptions.Public, isInGame.ToString().ToLowerInvariant())
                }
            };

            _currentLobby = await UGSLobbyService.Instance.UpdateLobbyAsync(_currentLobby.Id, options);
            LobbyChanged?.Invoke();
        }

        public async UniTask DeleteLobbyAsync()
        {
            if (_currentLobby == null) return;

            StopHeartbeat();
            var lobbyId = _currentLobby.Id;

            try
            {
                await UGSLobbyService.Instance.DeleteLobbyAsync(lobbyId);
                Debug.Log($"[LobbyService] Deleted lobby {lobbyId}");
            }
            catch (LobbyServiceException e)
            {
                Debug.LogWarning($"[LobbyService] Failed to delete lobby: {e.Message}");
            }

            _currentLobby = null;
            LobbyChanged?.Invoke();
        }

        public void Dispose()
        {
            StopHeartbeat();
        }

        private void StartHeartbeat()
        {
            StopHeartbeat();
            _heartbeatCts = new CancellationTokenSource();
            HeartbeatLoop(_heartbeatCts.Token).Forget();
        }

        private void StopHeartbeat()
        {
            _heartbeatCts?.Cancel();
            _heartbeatCts?.Dispose();
            _heartbeatCts = null;
        }

        private async UniTaskVoid HeartbeatLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _currentLobby != null)
            {
                try
                {
                    await UGSLobbyService.Instance.SendHeartbeatPingAsync(_currentLobby.Id);
                }
                catch (LobbyServiceException e)
                {
                    Debug.LogWarning($"[LobbyService] Heartbeat failed: {e.Message}");
                    break;
                }

                await UniTask.Delay(
                    TimeSpan.FromSeconds(HeartbeatIntervalSeconds),
                    cancellationToken: ct);
            }
        }

        /// <summary>
        /// UGS requires lobby passwords to be 8-64 chars. Hashing to SHA256 hex (64 chars)
        /// lifts that restriction from the user while staying deterministic across clients.
        /// </summary>
        private static string NormalizePassword(string password)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static Player MakePlayer()
        {
            return new Player(
                id: AuthenticationService.Instance.PlayerId,
                data: new Dictionary<string, PlayerDataObject>
                {
                    ["displayName"] = new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "Player")
                });
        }

        private static LobbyInfo ToLobbyInfo(Unity.Services.Lobbies.Models.Lobby lobby)
        {
            string relayCode = "";
            bool isInGame = false;
            bool hasPasswordFlag = false;

            if (lobby.Data != null)
            {
                if (lobby.Data.TryGetValue(KeyRelayJoinCode, out var codeObj))
                    relayCode = codeObj.Value;
                if (lobby.Data.TryGetValue(KeyIsInGame, out var inGameObj))
                    isInGame = inGameObj.Value == "true";
                if (lobby.Data.TryGetValue(KeyHasPassword, out var hpObj))
                    hasPasswordFlag = hpObj.Value == "true";
            }

            return new LobbyInfo
            {
                Id = lobby.Id,
                Name = lobby.Name,
                LobbyCode = lobby.LobbyCode,
                CurrentPlayers = lobby.Players?.Count ?? 0,
                MaxPlayers = lobby.MaxPlayers,
                IsPrivate = lobby.IsPrivate,
                IsPasswordProtected = hasPasswordFlag,
                IsInGame = isInGame,
                RelayJoinCode = relayCode,
                HostId = lobby.HostId
            };
        }
    }
}
