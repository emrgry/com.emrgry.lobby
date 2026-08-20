# Changelog

## [1.2.2] — 2026-08-20

### Fixed
- Lobby passwords are now enforced server-side via UGS native password support
  (`CreateLobbyOptions.Password`, `JoinLobbyByIdOptions.Password`,
  `JoinLobbyByCodeOptions.Password`). Previously the password was only stored as
  member-visible lobby data and never verified on join.
- Passwords are normalized to SHA256 hex (64 chars) so users may enter short
  passwords despite the UGS 8-64 char requirement.
- The plaintext member-visible `password` DataObject is gone; the public
  `hasPassword` flag remains for lock icons in browse lists.

### Added
- `LobbyPasswordException` — thrown by the join methods when the password is
  missing or incorrect.
- Optional `password` parameter on `ILobbyService.JoinLobbyByIdAsync` /
  `JoinLobbyByCodeAsync` and `LobbyConnectionFlow.JoinGameByIdAsync` /
  `JoinGameByCodeAsync`.

### Removed
- `LobbyInfo.Password` field (no longer exposed anywhere).

Also released as hotfix tag `v1.1.1` on top of v1.1.0 for projects not yet on the 1.2 line.

## [1.2.0] — 2026-05-11

### Added
- `LobbyBootstrap` MonoBehaviour — add-on bootstrap that subscribes to
  `NetworkBootstrap.UGSReady` and registers `ILobbyService` automatically.
- `LobbyConnectionFlow` — static helper combining lobby + relay flows
  (`HostGameAsync`, `JoinGameByIdAsync`, `JoinGameByCodeAsync`, `LeaveGameAsync`).
- `LobbySessionContext` — static cache of the current `LobbyInfo` so UI on a
  different scene than the create/join call site can read the lobby code/id.
- `ILobbyService.IsLocalPlayerHost` — convenience property for UI showing host-only
  controls.
- `ILobbyService.LobbyDataChanged` event — fired with the new `LobbyInfo`
  after `UpdateLobbyDataAsync` succeeds.

### Changed
- Package now depends on `com.emrgry.network` `2.0.0` and `com.emrgry.core` `1.1.0`
  because `LobbyConnectionFlow` orchestrates with `IConnectionService` and
  `INetworkSceneService`.

### Migration from v1.1.0

1. **Add `LobbyBootstrap`** to your Bootstrap scene next to `NetworkBootstrap`.
   You can now remove the manual `new LobbyService()` and `ServiceLocator.Register<ILobbyService>(...)` lines from your project.
2. **Replace project-side host/join helpers** with `LobbyConnectionFlow.HostGameAsync` /
   `JoinGameByIdAsync` / `LeaveGameAsync`. The signatures match the previous
   `ConnectionFlowHelper` pattern but live in the package.
3. **Use `LobbySessionContext.Current.LobbyCode`** for sharing the join code via UI.

## [1.1.0] — Previous release
- Password-protected lobbies, query, join by id/code, heartbeat.
