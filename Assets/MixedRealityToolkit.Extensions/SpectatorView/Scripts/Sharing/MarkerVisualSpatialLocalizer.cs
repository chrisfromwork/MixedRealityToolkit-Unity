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
        private TaskCompletionSource<bool> markerFound = null;
        private int markerId = 0;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

#if UNITY_EDITOR
        private void OnValidate()
        {
            FieldHelper.ValidateType<IMarkerVisual>(MarkerVisual);
        }
#endif

        protected override ISpatialCoordinateService SpatialCoordinateService => throw new NotImplementedException();

        private void Awake()
        {
            markerVisual = MarkerVisual as IMarkerVisual;

            if (markerVisual == null ||
                cameraTransform == null)
            {
                throw new ArgumentNullException("Needed arguments weren't specified.");
            }

            var markerToCamera = Matrix4x4.TRS(markerVisualPosition, Quaternion.Euler(markerVisualRotation), Vector3.one);
            markerVisualCoordinateService = new MarkerVisualCoordinateService(markerVisual, markerToCamera, cameraTransform);
        }

        internal async override Task<Guid> InitializeAsync(bool actAsHost, CancellationToken cancellationToken)
        {
            Guid token = Guid.NewGuid();
            markerFound?.SetCanceled();
            markerFound = new TaskCompletionSource<bool>();
            return token;
        }

        internal async override Task<ISpatialCoordinate> LocalizeAsync(bool actAsHost, Guid token, Action<Action<BinaryWriter>> writeAndSendMessage, CancellationToken cancellationToken)
        {
            lock(cancellationTokenSource)
            {
                if (cancellationTokenSource.Token.CanBeCanceled)
                {
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource.Dispose();
                }

                cancellationTokenSource = new CancellationTokenSource();
            }

            ISpatialCoordinate coordinate = null;
            if (await markerVisualCoordinateService.TryDiscoverCoordinatesAsync(cancellationTokenSource.Token, new string[] { markerId.ToString() }))
            {
                if (!markerVisualCoordinateService.TryGetKnownCoordinate(markerId.ToString(), out coordinate))
                {
                    DebugLog("Failed to find spatial coordinate although discovery completed.", token);
                }
            }

            await Task.WhenAny(markerFound.Task, Task.Delay(-1, cancellationToken));

            lock(cancellationTokenSource)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
            }

            return coordinate;
        }

        internal override void ProcessIncomingMessage(bool actAsHost, Guid token, string command, BinaryReader r)
        {
            markerFound.TrySetResult(true);
        }

        internal override void Uninitialize(bool actAsHost, Guid token)
        {
            lock (cancellationTokenSource)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
            }
        }
    }
}
