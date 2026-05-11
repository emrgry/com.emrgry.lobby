# com.emrgry.lobby

UGS-backed lobby service with drop-in bootstrap and high-level
`LobbyConnectionFlow` helpers that combine lobby + relay into one call.

## Requirements

- Unity 6000.0+
- [UniTask](https://github.com/Cysharp/UniTask) (git URL install)
- [com.emrgry.core](https://github.com/emrgry/com.emrgry.core) `1.1.0+`
- [com.emrgry.network](https://github.com/emrgry/com.emrgry.network) `2.0.0+`
- Unity Services Multiplayer (`com.unity.services.multiplayer`) `2.1.3+`

## Installation Order

```
# UniTask
https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask

# core
https://github.com/emrgry/com.emrgry.core.git#v1.1.0

# network
https://github.com/emrgry/com.emrgry.network.git#v2.0.0

# lobby
https://github.com/emrgry/com.emrgry.lobby.git#v1.2.0
```

## Quick start

1. **Bootstrap scene** — already has `NetworkBootstrap` (from the network pkg).
   Add a sibling **`LobbyBootstrap`** component. That's it — `ILobbyService` will
   register itself once UGS sign-in finishes.
2. **Host UI button**:
   ```csharp
   var result = await LobbyConnectionFlow.HostGameAsync(
       lobbyName: "My Lobby",
       maxPlayers: 4,
       usePassword: false,
       password: null,
       lobbySceneName: "LobbyScene");

   if (result.Success)
       Debug.Log($"Lobby code: {result.LobbyInfo.LobbyCode}");
   ```
3. **Join UI button** (from browse list):
   ```csharp
   var result = await LobbyConnectionFlow.JoinGameByIdAsync(lobbyId);
   ```
4. **Join by code** (manual entry):
   ```csharp
   var result = await LobbyConnectionFlow.JoinGameByCodeAsync(shortCode);
   ```
5. **Leave**:
   ```csharp
   await LobbyConnectionFlow.LeaveGameAsync(deleteIfHost: true);
   ```
6. **Read current lobby anywhere** (e.g. in a LobbyScreen):
   ```csharp
   if (LobbySessionContext.HasValue)
       _codeText.text = LobbySessionContext.Current.LobbyCode;
   ```

## Contents

| Type | Description |
|------|-------------|
| `ILobbyService` / `LobbyService` | UGS Lobby wrapper. Create / query / join (id+code) / update / leave / delete + heartbeat. |
| `LobbyBootstrap` | MonoBehaviour add-on. Registers `ILobbyService` after `NetworkBootstrap.UGSReady`. |
| `LobbyConnectionFlow` | Static helper combining lobby + relay flows. |
| `LobbySessionContext` | Static cache of current `LobbyInfo` across scene loads. |
| `LobbyInfo` | DTO with Id, Name, LobbyCode, IsPasswordProtected, RelayJoinCode, etc. |
| `LobbyHostResult` / `LobbyJoinResult` | Result structs for flow methods. |

## Password-protected lobbies

A password-protected lobby stays publicly listed in `QueryLobbiesAsync` so it
shows up in browse, but the join call requires a password. The password is
stored as Member-visibility lobby data; the `hasPassword` flag is public so
your UI can show a lock icon.
