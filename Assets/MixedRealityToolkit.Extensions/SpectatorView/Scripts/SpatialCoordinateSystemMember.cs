// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Experimental.SpatialAlignment.Common;
using Microsoft.MixedReality.Toolkit.Extensions.Experimental.Socketer;
using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Experimental.SpectatorView
{
    /// <summary>
    /// The SpectatorView helper class for managing a participant in the spatial coordinate system
    /// </summary>
    internal class SpatialCoordinateSystemMember : DisposableBase
    {
        public const string SpatialCoordinateSystemMemberMessageHeader = "MEMBER";
        public ISpatialCoordinate spatialCoordinate = null;
        public Vector3 OriginPositionInCoordinateSpace = Vector3.zero;
        public Quaternion OriginRotationInCoordinateSpace = Quaternion.identity;

        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly CancellationToken cancellationToken;

        private readonly Role role;
        private readonly SocketEndpoint socketEndpoint;
        private readonly Camera unityCamera;
        private readonly Func<GameObject> createSpatialCoordinateGO;
        private readonly bool debugLogging;
        private bool showDebugVisuals = false;
        private GameObject debugVisual = null;
        private float debugVisualScale = 1.0f;

        private Action<string, BinaryReader> processIncomingMessages = null;
        private GameObject spatialCoordinateGO = null;

        /// <summary>
        /// Instantiates a new <see cref="SpatialCoordinateSystemMember"/>.
        /// </summary>
        /// <param name="role">The role of the current device (is it a Broadcaster adding a connected observer to it's list).</param>
        /// <param name="socketEndpoint">The endpoint of the other entity.</param>
        /// <param name="createSpatialCoordinateGO">The function that creates a spatial coordinate game object on detection<see cref="GameObject"/>.</param>
        /// <param name="debugLogging">Flag for enabling troubleshooting logging.</param>
        public SpatialCoordinateSystemMember(Role role, SocketEndpoint socketEndpoint, Func<GameObject> createSpatialCoordinateGO, bool debugLogging, bool showDebugVisuals = false, GameObject debugVisual = null, float debugVisualScale = 1.0f)
        {
            cancellationToken = cancellationTokenSource.Token;

            this.role = role;
            this.socketEndpoint = socketEndpoint;
            this.createSpatialCoordinateGO = createSpatialCoordinateGO;
            this.debugLogging = debugLogging;
            this.showDebugVisuals = showDebugVisuals;
            this.debugVisual = debugVisual;
            this.debugVisualScale = debugVisualScale;
        }

        private void DebugLog(string message)
        {
            if (debugLogging)
            {
                Debug.Log($"SpatialCoordinateSystemMember [{role} - Connection: {socketEndpoint.Address}]: {message}");
            }
        }

        /// <summary>
        /// Localizes this observer with the connected party using the given mechanism.
        /// </summary>
        /// <param name="localizationMechanism">The mechanism to use for localization.</param>
        public async Task LocalizeAsync(SpatialLocalizer spatialLocalizer)
        {
            DebugLog("Started LocalizeAsync");

            DebugLog("Initializing with LocalizationMechanism.");
            // Tell the localization mechanism to initialize, this could create anchor if need be
            Guid token = await spatialLocalizer.InitializeAsync(role, cancellationToken);
            DebugLog("Initialized with LocalizationMechanism");

            try
            {
                lock (cancellationTokenSource)
                {
                    processIncomingMessages = (command, reader) =>
                    {
                        DebugLog("Passing on incoming message");
                        if(!TryProcessIncomingMessage(command, reader))
                        {
                            spatialLocalizer.ProcessIncomingMessage(role, token, command, reader);
                        }
                    };
                }

                DebugLog("Telling LocalizationMechanims to begin localizng");
                spatialCoordinate = await spatialLocalizer.LocalizeAsync(role, token, WriteAndSendMessage, cancellationToken);

                if (spatialCoordinate == null)
                {
                    Debug.LogError($"Failed to localize for spectator: {socketEndpoint.Address}");
                }
                else
                {
                    DebugLog("Creating Visual for spatial coordinate");
                    lock (cancellationTokenSource)
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            // TODO - figure out where this transform should be applied. It may be more practical in the spatial coordinate system manager.
                            spatialCoordinateGO = createSpatialCoordinateGO();
                            var spatialCoordinateLocalizer = spatialCoordinateGO.AddComponent<SpatialCoordinateLocalizer>();
                            spatialCoordinateLocalizer.debugLogging = debugLogging;
                            spatialCoordinateLocalizer.showDebugVisuals = showDebugVisuals;
                            spatialCoordinateLocalizer.debugVisual = debugVisual;
                            spatialCoordinateLocalizer.debugVisualScale = debugVisualScale;
                            spatialCoordinateLocalizer.Coordinate = spatialCoordinate;
                            DebugLog("Spatial coordinate created, coordinate set");
                        }
                    }
                }
            }
            finally
            {
                lock (cancellationTokenSource)
                {
                    processIncomingMessages = null;
                }

                DebugLog("Uninitializing.");
                spatialLocalizer.Uninitialize(role, token);
                DebugLog("Uninitialized.");
            }
        }

        /// <summary>
        /// Handles messages received from the network.
        /// </summary>
        /// <param name="reader">The reader to access the contents of the message.</param>
        public void ReceiveMessage(string command, BinaryReader reader)
        {
            lock (cancellationTokenSource)
            {
                processIncomingMessages?.Invoke(command, reader);
            }
        }

        protected override void OnManagedDispose()
        {
            base.OnManagedDispose();

            DebugLog("Disposed");

            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();

            lock (cancellationTokenSource)
            {
                UnityEngine.Object.Destroy(spatialCoordinateGO);
            }
        }

        private void SendState()
        {
            DebugLog("Sending state information");
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write(SpatialCoordinateSystemMemberMessageHeader);
                writer.Write(spatialCoordinate.WorldToCoordinateSpace(Vector3.zero));
                writer.Write(spatialCoordinate.WorldToCoordinateSpace(Quaternion.identity));
                socketEndpoint.Send(memoryStream.ToArray());
            }
        }

        private bool TryProcessIncomingMessage(string command, BinaryReader reader)
        {
            if (command == SpatialCoordinateSystemMemberMessageHeader)
            {
                OriginPositionInCoordinateSpace = reader.ReadVector3();
                OriginRotationInCoordinateSpace = reader.ReadQuaternion();
                DebugLog($"Obtained SpatialCoordinateSystemMember origin in coordinate space. Position: {OriginPositionInCoordinateSpace.ToString()}, Rotation: {OriginRotationInCoordinateSpace.ToString()}");
            }

            return false;
        }

        private void WriteAndSendMessage(Action<BinaryWriter> callToWrite)
        {
            DebugLog("Sending message to connected client");
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write(SpatialCoordinateSystemManager.SpatialCoordinateSystemMessageHeader);

                // Allow the spatialLocalizer to write its own content to the binary writer with this function
                callToWrite(writer);

                socketEndpoint.Send(memoryStream.ToArray());
                DebugLog("Sent Message");
            }
        }
    }
}
