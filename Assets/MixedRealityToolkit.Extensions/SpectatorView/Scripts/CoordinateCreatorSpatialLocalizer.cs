// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Experimental.SpatialAlignment.Common;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Toolkit.Extensions.Experimental.SpectatorView.MarkerDetection
{
    internal abstract class CoordinateCreatorSpatialLocalizer : SpatialLocalizer
    {
        protected ISpatialCoordinateService spatialCoordinateService = null;
        private Task<ISpatialCoordinate> initializeUserCoordinateTask = null;
        private TaskCompletionSource<string> observerCoordinateIdToLookFor = null;
        private readonly object lockObject = new object();

        /// <summary>
        /// The logic for the host to figure out which coordinate to use for localizing with observer.
        /// </summary>
        /// <param name="token">The token that first requested this host coordinate.</param>
        /// <returns>The spatial coordinate.</returns>
        protected abstract Task<ISpatialCoordinate> CreateCoordinateAsync(Guid token);

        /// <inheritdoc/>
        internal async override Task<Guid> InitializeAsync(CancellationToken cancellationToken)
        {
            Guid token = Guid.NewGuid();
            DebugLog("Begining initialization", token);

            DebugLog("User", token);
            lock (lockObject)
            {
                DebugLog("Checking for host init task", token);
                if (initializeUserCoordinateTask == null)
                {
                    DebugLog("Creating new host init task", token);
                    initializeUserCoordinateTask = CreateCoordinateAsync(token);
                    DebugLog("Host init task created", token);
                }
            }

            DebugLog("Waiting for init or cancellation.", token);
            // Wait for broadcaster to initialize (which happens once and won't be cancelled), or until this request was cancelled.
            await Task.WhenAny(Task.Delay(-1, cancellationToken), initializeUserCoordinateTask);
            DebugLog("Got Init task finished", token);
            //We have the coordinate after this step has finished

            DebugLog($"Added guid and returning.", token);
            return token;
        }

        /// <inheritdoc/>
        internal override void ProcessIncomingMessage(Guid token, BinaryReader r)
        {
            DebugLog("Dropping incoming message", token);
        }

        /// <inheritdoc/>
        internal override async Task<ISpatialCoordinate> LocalizeAsync(Guid token, Action<Action<BinaryWriter>> writeAndSendMessage, CancellationToken cancellationToken)
        {
            DebugLog("Beginning spatial localization", token);
            ISpatialCoordinate coordinateToReturn = null;

            DebugLog("User getting initialized coordinate", token);
            coordinateToReturn = initializeUserCoordinateTask.Result;
            DebugLog($"Sending coordinate id: {coordinateToReturn.Id}", token);
            writeAndSendMessage(writer => writer.Write(coordinateToReturn.Id));
            DebugLog("Message sent.", token);
            await Task.Delay(1, cancellationToken).IgnoreCancellation(); // Wait a frame, this is how Unity synchronization context will let you wait for next frame
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
