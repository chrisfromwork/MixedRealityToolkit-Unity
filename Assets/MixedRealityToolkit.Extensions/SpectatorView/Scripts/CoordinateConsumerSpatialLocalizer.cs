// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Experimental.SpatialAlignment.Common;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Experimental.SpectatorView.MarkerDetection
{
    internal abstract class CoordinateConsumerSpatialLocalizer : SpatialLocalizer
    {
        protected ISpatialCoordinateService spatialCoordinateService = null;
        private Task<ISpatialCoordinate> initializeUserCoordinateTask = null;
        private TaskCompletionSource<string> observerCoordinateIdToLookFor = null;
        private readonly object lockObject = new object();

        /// <inheritdoc/>
        internal async override Task<Guid> InitializeAsync(CancellationToken cancellationToken)
        {
            Guid token = Guid.NewGuid();
            DebugLog("Begining initialization", token);
            observerCoordinateIdToLookFor?.SetCanceled();
            observerCoordinateIdToLookFor = new TaskCompletionSource<string>();
            await Task.Delay(1, cancellationToken).IgnoreCancellation(); // Wait a frame, this is how Unity synchronization context will let you wait for next frame
            DebugLog($"Added guid and returning.", token);
            return token;
        }

        /// <inheritdoc/>
        internal override void ProcessIncomingMessage(Guid token, BinaryReader r)
        {
            DebugLog("Processing incoming message", token);
            string result = r.ReadString();
            DebugLog($"Incoming message string: {result}, setting as coordinate id.", token);
            observerCoordinateIdToLookFor.TrySetResult(result);
            DebugLog("Set coordinate id.", token);
        }

        /// <inheritdoc/>
        internal override async Task<ISpatialCoordinate> LocalizeAsync(Guid token, Action<Action<BinaryWriter>> writeAndSendMessage, CancellationToken cancellationToken)
        {
            DebugLog("Beginning spatial localization", token);
            ISpatialCoordinate coordinateToReturn = null;

            DebugLog("Spectator waiting for coord id to be sent over", token);
            await Task.WhenAny(observerCoordinateIdToLookFor.Task, Task.Delay(-1, cancellationToken)); //If we get cancelled, or get a token

            DebugLog("Coordinate id received, reading.", token);
            // Now we have coordinateId in TaskCompletionSource
            string id = observerCoordinateIdToLookFor.Task.Result;
            DebugLog($"Coordinate id: {id}, starting discovery.", token);

            if (await SpatialCoordinateService.TryDiscoverCoordinatesAsync(cancellationToken, id))
            {
                DebugLog("Discovery complete, retrieving reference to ISpatialCoordinate", token);
                if (!SpatialCoordinateService.TryGetKnownCoordinate(id, out coordinateToReturn))
                {
                    Debug.LogError("We discovered, but for some reason failed to get coordinate from service.");
                }
            }
            else
            {
                Debug.LogError("Failed to discover spatial coordinate.");
            }

            DebugLog("Returning coordinate.", token);
            return coordinateToReturn;
        }

        /// <inheritdoc/>
        internal override void Uninitialize(Guid token)
        {
            DebugLog($"Deinitializing", token);
        }
    }
}
