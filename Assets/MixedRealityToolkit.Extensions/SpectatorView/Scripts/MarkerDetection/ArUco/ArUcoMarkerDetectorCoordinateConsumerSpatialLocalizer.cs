using Microsoft.MixedReality.Experimental.SpatialAlignment.Common;
using Microsoft.MixedReality.Toolkit.Extensions.Experimental.SpectatorView.MarkerDetection;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Experimental.SpectatorView
{
    internal class ArUcoMarkerDetectorCoordinateConsumerSpatialLocalizer : CoordinateConsumerSpatialLocalizer
    {
        [Tooltip("The reference to Aruco marker detector.")]
        [SerializeField]
        private SpectatorViewPluginArUcoMarkerDetector arucoMarkerDetector = null;

        /// <inheritdoc/>
        protected override ISpatialCoordinateService SpatialCoordinateService => spatialCoordinateService;

        private void Awake()
        {
            spatialCoordinateService = new MarkerDetectorCoordinateService(arucoMarkerDetector, debugLogging);
        }
    }
}