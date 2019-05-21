// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Experimental.SpatialAlignment.Common;
using Microsoft.MixedReality.Toolkit.Extensions.Experimental.SpectatorView.MarkerDetection;

namespace Microsoft.MixedReality.Toolkit.Extensions.Experimental.SpectatorView
{
    /// <summary>
    /// SpatialLocalizer that shows an ArUco marker
    /// </summary>
    internal class ArUcoMarkerVisualizerSpatialLocalizer : CoordinateConsumerSpatialLocalizer
    {
        /// <inheritdoc/>
        protected override ISpatialCoordinateService SpatialCoordinateService => spatialCoordinateService;

        private void Awake()
        {
            spatialCoordinateService = new MarkerVisualizerCoordinateService(null, null, null);
        }
    }
}
