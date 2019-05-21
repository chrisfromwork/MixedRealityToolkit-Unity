// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Experimental.SpatialAlignment.Common;
using Microsoft.MixedReality.Toolkit.Extensions.Experimental.SpectatorView.MarkerDetection;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Experimental.SpectatorView
{
    internal class ArUcoMarkerDetectorCoordinateCreatorSpatialLocalizer : CoordinateCreatorSpatialLocalizer
    {
        [Tooltip("The reference to Aruco marker detector.")]
        [SerializeField]
        private SpectatorViewPluginArUcoMarkerDetector arucoMarkerDetector = null;

        /// <inheritdoc/>
        protected override ISpatialCoordinateService SpatialCoordinateService => spatialCoordinateService;

        protected override async Task<ISpatialCoordinate> CreateCoordinateAsync(Guid token)
        {
            DebugLog("Getting host coordinate", token);
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                TaskCompletionSource<ISpatialCoordinate> coordinateTCS = new TaskCompletionSource<ISpatialCoordinate>();
                void coordinateDiscovered(ISpatialCoordinate coord)
                {
                    DebugLog("Coordinate found", token);
                    coordinateTCS.SetResult(coord);
                    cts.Cancel();
                }

                SpatialCoordinateService.CoordinatedDiscovered += coordinateDiscovered;
                try
                {
                    DebugLog("Starting to look for coordinates", token);
                    await SpatialCoordinateService.TryDiscoverCoordinatesAsync(cts.Token);
                    DebugLog("Stopped looking for coordinates", token);


                    DebugLog("Awaiting found coordiante", token);
                    // Don't necessarily need to await here
                    return await coordinateTCS.Task;
                }
                finally
                {
                    DebugLog("Unsubscribing from coordinate discovered", token);
                    SpatialCoordinateService.CoordinatedDiscovered -= coordinateDiscovered;
                }
            }
        }

        private void Awake()
        {
            spatialCoordinateService = new MarkerDetectorCoordinateService(arucoMarkerDetector, debugLogging);
        }
    }
}
