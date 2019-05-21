﻿using Microsoft.MixedReality.Experimental.SpatialAlignment.Common;
using Microsoft.MixedReality.Toolkit.Extensions.Experimental.Socketer;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Experimental.SpectatorView
{
    public class SpatialCoordinateSystemManager : Singleton<SpatialCoordinateSystemManager>,
        ICommandHandler
    {
        /// <summary>
        /// SpectatorView MonoBehaviour running on the device.
        /// </summary>
        [Tooltip("SpectatorView MonoBehaviour running on the device.")]
        [SerializeField]
        private SpectatorView spectatorView = null;

        /// <summary>
        /// Scene root game object.
        /// </summary>
        [Tooltip("Scene root game object.")]
        [SerializeField]
        private GameObject sceneRoot = null;

        /// <summary>
        /// Check for debug logging.
        /// </summary>
        [Tooltip("Check for debug logging.")]
        [SerializeField]
        private bool debugLogging = false;

        public const string SpatialLocalizationMessageHeader = "LOCALIZE";
        readonly string[] supportedCommands = { SpatialLocalizationMessageHeader };
        private Dictionary<SocketEndpoint, SpatialCoordinateSystemMember> members = new Dictionary<SocketEndpoint, SpatialCoordinateSystemMember>();
        private SpatialLocalizer spatialLocalizer = null;

        public void OnConnected(SocketEndpoint endpoint)
        {
            if (members.ContainsKey(endpoint))
            {
                Debug.LogWarning("SpatialCoordinateSystemMember connected that already existed");
                return;
            }

            if (spectatorView.Role == Role.Spectator)
            {
                if (members.Count > 0 &&
                    !members.ContainsKey(endpoint))
                {
                    Debug.LogWarning("A second SpatialCoordinateSystemMember connected while the device was running as a spectator. This is an unexpected scenario.");
                    return;
                }
            }

            DebugLog($"Creating new SpatialCoordinateSystemMember, Role: {spectatorView.Role}, IPAddress: {endpoint.Address}, SceneRoot: {sceneRoot}, DebugLogging: {debugLogging}");
            var member = new SpatialCoordinateSystemMember(spectatorView.Role, endpoint, () => sceneRoot, debugLogging);
            members[endpoint] = member;
            if (spatialLocalizer != null)
            {
                DebugLog($"Localizing SpatialCoordinateSystemMember: {endpoint.Address}");
                member.LocalizeAsync(spatialLocalizer).FireAndForget();
            }
            else
            {
                Debug.LogWarning("Spatial localizer not specified for SpatialCoordinateSystemManager");
            }
        }

        public void OnDisconnected(SocketEndpoint endpoint)
        {
            if (members.TryGetValue(endpoint, out var member))
            {
                member.Dispose();
                members.Remove(endpoint);
            }
        }

        public void HandleCommand(SocketEndpoint endpoint, string command, BinaryReader reader)
        {
            switch (command)
            {
                case SpatialLocalizationMessageHeader:
                    {
                        if (!members.TryGetValue(endpoint, out var member))
                        {
                            Debug.LogError("Received a message for an endpoint that had no associated spatial coordinate system member");
                        }
                        else
                        {
                            member.ReceiveMessage(reader);
                        }
                    }
                    break;
            }
        }

        protected override void Awake()
        {
            RegisterCommands();

            // For now, users will be the coordinate creator, spectators will be coordinate consumers.
            spatialLocalizer = (spectatorView.Role == Role.User) ?
                (SpatialLocalizer) GetComponent<ArUcoMarkerDetectorCoordinateCreatorSpatialLocalizer>() :
                (SpatialLocalizer) GetComponent<ArUcoMarkerDetectorCoordinateConsumerSpatialLocalizer>();
        }

        protected override void OnDestroy()
        {
            UnregisterCommands();
            CleanUpMembers();
        }

        private void RegisterCommands()
        {
            DebugLog($"Registering for appropriate commands: CommandService.IsInitialized: {CommandService.IsInitialized}");
            foreach (var command in supportedCommands)
            {
                CommandService.Instance.RegisterCommandHandler(command, this);
            }
        }

        private void UnregisterCommands()
        {
            DebugLog($"Unregistering for appropriate commands: CommandService.IsInitialized: {CommandService.IsInitialized}");
            foreach (var command in supportedCommands)
            {
                CommandService.Instance.UnregisterCommandHandler(command, this);
            }
        }

        private void CleanUpMembers()
        {
            foreach(var member in members)
            {
                member.Value.Dispose();
            }

            members.Clear();
        }

        private void DebugLog(string message)
        {
            if (debugLogging)
            {
                Debug.Log($"SpatialCoordinateSystemManager: {message}");
            }
        }
    }
}
