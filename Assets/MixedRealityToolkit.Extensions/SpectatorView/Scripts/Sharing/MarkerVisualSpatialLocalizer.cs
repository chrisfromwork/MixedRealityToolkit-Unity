// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.MixedReality.Experimental.SpatialAlignment.Common;
using Microsoft.MixedReality.Toolkit.Extensions.Experimental.MarkerDetection;
using Microsoft.MixedReality.Toolkit.Extensions.Experimental.SpectatorView.Utilities;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Experimental.SpectatorView
{
    /// <summary>
    /// SpatialLocalizer that shows a marker
    /// </summary>
    internal class MarkerVisualSpatialLocalizer : SpatialLocalizer
    {
        [Tooltip("The reference to an IMarkerVisual GameObject.")]
        [SerializeField]
        private MonoBehaviour MarkerVisual = null;
        private IMarkerVisual markerVisual = null;

        [Tooltip("The reference to the camera transform.")]
        [SerializeField]
        private Transform cameraTransform;

        [Tooltip("Marker Visual poosition relative to the device camera.")]
        [SerializeField]
        private Vector3 markerVisualPosition = Vector3.zero;

        [Tooltip("Marker Visual Rotation relative to the device camera.")]
        [SerializeField]
        private Vector3 markerVisualRotation = new Vector3(0, 180, 0);

        private MarkerVisualCoordinateService markerVisualCoordinateService = null;
        private TaskCompletionSource<string> coordinateAssigned = null;
        private TaskCompletionSource<string> coordinateFound = null;
        private CancellationTokenSource discoveryCTS = new CancellationTokenSource();
        private string coordinateId = string.Empty;

        public const string MarkerVisualDiscoveryHeader = "MARKERVISUALLOC";
        public const string CoordinateAssignedHeader = "ASSIGNID";
        public const string CoordinateFoundHeader = "COORDFOUND";

#if UNITY_EDITOR
        private void OnValidate()
        {
            FieldHelper.ValidateType<IMarkerVisual>(MarkerVisual);
        }
#endif

        protected override ISpatialCoordinateService SpatialCoordinateService => markerVisualCoordinateService;

        private void Awake()
        {
            markerVisual = MarkerVisual as IMarkerVisual;

            if (markerVisual == null ||
                cameraTransform == null)
            {
                throw new ArgumentNullException("Needed arguments weren't specified.");
            }

            var markerToCamera = Matrix4x4.TRS(markerVisualPosition, Quaternion.Euler(markerVisualRotation), Vector3.one);
            markerVisualCoordinateService = new MarkerVisualCoordinateService(markerVisual, markerToCamera, cameraTransform, debugLogging);
        }

        internal override Task<Guid> InitializeAsync(bool actAsHost, CancellationToken cancellationToken)
        {
            Guid token = Guid.NewGuid();

            coordinateId = string.Empty;
            coordinateAssigned?.SetCanceled();
            coordinateAssigned = new TaskCompletionSource<string>();

            coordinateFound?.SetCanceled();
            coordinateFound = new TaskCompletionSource<string>();

            DebugLog("Initialized", token);
            return Task.FromResult(token);
        }

        internal async override Task<ISpatialCoordinate> LocalizeAsync(bool actAsHost, Guid token, Action<Action<BinaryWriter>> writeAndSendMessage, CancellationToken cancellationToken)
        {
            DebugLog("Localizing", token);
            if (!TrySendMarkerVisualDiscoveryMessage(writeAndSendMessage))
            {
                Debug.LogWarning("Failed to send marker visual discovery message, spatial localization failed.");
                return null;
            }

            // Receive marker to show
            DebugLog("Waiting to have a coordinate id assigned", token);
            await Task.WhenAny(coordinateAssigned.Task, Task.Delay(-1, cancellationToken));
            if (coordinateId == string.Empty)
            {
                DebugLog("Failed to assign coordinate id", token);
                return null;
            }

            DebugLog($"Coordinate assigned: {coordinateId}", token);
            ISpatialCoordinate coordinate = null;
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(discoveryCTS.Token, cancellationToken))
            {
                DebugLog($"Attempting to discover the associated marker: {coordinateId}", token);
                if (await markerVisualCoordinateService.TryDiscoverCoordinatesAsync(cts.Token, new string[] { coordinateId.ToString() }))
                {
                    DebugLog($"Marker discovery completed: {coordinateId}", token);
                    if (!markerVisualCoordinateService.TryGetKnownCoordinate(coordinateId, out coordinate))
                    {
                        DebugLog("Failed to find spatial coordinate although discovery completed.", token);
                    }
                }
            }

            // Wait for marker to be found
            DebugLog($"Waiting for the coordinate to be found: {coordinateId}", token);
            await Task.WhenAny(coordinateFound.Task, Task.Delay(-1, cancellationToken));

            return coordinate;
        }

        internal override void ProcessIncomingMessage(bool actAsHost, Guid token, string command, BinaryReader r)
        {
            DebugLog($"Received command: {command}", token);
            switch (command)
            {
                case CoordinateAssignedHeader:
                    coordinateId = r.ReadString();
                    DebugLog($"Assigned coordinate id: {coordinateId}", token);
                    coordinateAssigned?.SetResult(coordinateId);
                    break;
                case CoordinateFoundHeader:
                    discoveryCTS.Cancel();
                    string detectedId = r.ReadString();
                    if (coordinateId == detectedId)
                    {
                        DebugLog($"Coordinate was found: {coordinateId}", token);
                        coordinateFound?.SetResult(detectedId);
                    }
                    else
                    {
                        DebugLog($"Unexpected coordinate found, expected: {coordinateId}, detected: {detectedId}", token);
                    }
                    break;
                default:
                    DebugLog($"Sent unknown command: {command}", token);
                    break;
            }
        }

        internal override void Uninitialize(bool actAsHost, Guid token)
        {
        }

        private bool TrySendMarkerVisualDiscoveryMessage(Action<Action<BinaryWriter>> writeAndSendMessage)
        {
            if (markerVisual.TryGetMaxId(out var maxId))
            {
                DebugLog($"Sending maximum id for discovery: {maxId}", Guid.NewGuid()); // todo - fix
                writeAndSendMessage(writer =>
                {
                    writer.Write(MarkerVisualDiscoveryHeader);
                    writer.Write(maxId);
                });

                return true;
            }

            DebugLog("Unable to obtain max id from marker visual", Guid.NewGuid()); // add token or remove token
            return false;
        }
    }
}
