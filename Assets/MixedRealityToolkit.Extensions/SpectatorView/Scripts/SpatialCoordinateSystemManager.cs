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
        /// SpatialLocalizer used for setting up the coordinate system.
        /// </summary>
        [Tooltip("SpatialLocalizer used for setting up the coordinate system.")]
        [SerializeField]
        private SpatialLocalizer spatialLocalizer = null;

        /// <summary>
        /// GameObject that is transformed to move content into the correct position within the spatial coordinate system.
        /// </summary>
        public GameObject transformedGameObject { get; set; }

        /// <summary>
        /// Check for debug logging.
        /// </summary>
        [Tooltip("Check for debug logging.")]
        [SerializeField]
        private bool debugLogging = false;

        /// <summary>
        /// Check to show debug visuals.
        /// </summary>
        [Tooltip("Check to show debug visuals.")]
        public bool showDebugVisuals = false;

        /// <summary>
        /// Game Object to render at spatial coordinate locations when showing debug visuals.
        /// </summary>
        [Tooltip("Game Object to render at spatial coordinate locations when showing debug visuals.")]
        public GameObject debugVisual = null;

        /// <summary>
        /// Debug visual scale.
        /// </summary>
        [Tooltip("Debug visual scale.")]
        public float debugVisualScale = 1.0f;

        public const string SpatialCoordinateSystemMessageHeader = "SPATIALCOORDSYS";
        public const string SpatialCoordinateSystemCameraMessageHeader = "SPATIALCOORDSYSCAM";
        readonly string[] supportedCommands = { SpatialCoordinateSystemMessageHeader };
        private Dictionary<SocketEndpoint, SpatialCoordinateSystemMember> members = new Dictionary<SocketEndpoint, SpatialCoordinateSystemMember>();

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

            DebugLog($"Creating new SpatialCoordinateSystemMember, Role: {spectatorView.Role}, IPAddress: {endpoint.Address}, SceneRoot: {transformedGameObject}, DebugLogging: {debugLogging}");
            var member = new SpatialCoordinateSystemMember(spectatorView.Role, endpoint, () => new GameObject(), debugLogging, showDebugVisuals, debugVisual, debugVisualScale);
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
            if (command == SpatialCoordinateSystemMessageHeader)
            {
                string subCommand = reader.ReadString();

                if (subCommand == SpatialCoordinateSystemCameraMessageHeader)
                {
                    // TODO - handle camera update information
                }
                else
                {
                    if (!members.TryGetValue(endpoint, out var member))
                    {
                        Debug.LogError("Received a message for an endpoint that had no associated spatial coordinate system member");
                    }
                    else
                    {
                        member.ReceiveMessage(subCommand, reader);
                    }
                }
            }
        }

        protected override void Awake()
        {
            RegisterCommands();
        }

        protected override void OnDestroy()
        {
            UnregisterCommands();
            CleanUpMembers();
        }

        private void Update()
        {
            SendCameraReport();
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

        private void SendCameraReport()
        {
            // TOOD - send camera update, add both SpatialCoordinateSystemMessageHeader and SpatialCoordinateSystemCameraMessageHeader headers
        }
    }
}
