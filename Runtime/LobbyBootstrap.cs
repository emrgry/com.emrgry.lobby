using Emrgry.Core;
using Emrgry.Network.Bootstrap;
using UnityEngine;

namespace Emrgry.Lobby
{
    /// <summary>
    /// Add-on bootstrap for the lobby package. Place this on the same GameObject as
    /// (or a sibling of) <see cref="NetworkBootstrap"/>. It subscribes to
    /// <see cref="NetworkBootstrap.UGSReady"/> and registers an <see cref="ILobbyService"/>
    /// on the ServiceLocator once UGS sign-in completes.
    ///
    /// <para>Doing it this way keeps <c>com.emrgry.network</c> ignorant of the lobby
    /// package while still providing a single-component bootstrap experience for
    /// projects that opt into lobbies.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyBootstrap : MonoBehaviour
    {
        private LobbyService _lobbyService;
        private NetworkBootstrap _networkBootstrap;

        private void Awake()
        {
            _networkBootstrap = FindFirstObjectByType<NetworkBootstrap>();
            if (_networkBootstrap == null)
            {
                Debug.LogError("[LobbyBootstrap] No NetworkBootstrap in the scene. Add one before LobbyBootstrap.");
                enabled = false;
                return;
            }

            if (_networkBootstrap.IsInitialized) RegisterLobby();
            else                                  _networkBootstrap.UGSReady += RegisterLobby;
        }

        private void RegisterLobby()
        {
            _networkBootstrap.UGSReady -= RegisterLobby;

            if (ServiceLocator.TryResolve<ILobbyService>(out _))
            {
                Debug.LogWarning("[LobbyBootstrap] ILobbyService already registered. Skipping.");
                return;
            }

            _lobbyService = new LobbyService();
            ServiceLocator.Register<ILobbyService>(_lobbyService);
            Debug.Log("[LobbyBootstrap] LobbyService registered.");
        }

        private void OnDestroy()
        {
            if (_networkBootstrap != null) _networkBootstrap.UGSReady -= RegisterLobby;

            _lobbyService?.Dispose();
            if (ServiceLocator.TryResolve<ILobbyService>(out _))
                ServiceLocator.Deregister<ILobbyService>();
        }
    }
}
