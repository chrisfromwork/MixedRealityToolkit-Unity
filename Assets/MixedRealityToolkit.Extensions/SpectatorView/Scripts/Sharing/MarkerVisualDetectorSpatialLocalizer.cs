// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.MixedReality.Experimental.SpatialAlignment.Common;
using Microsoft.MixedReality.Toolkit.Extensions.Experimental.MarkerDetection;
using Microsoft.MixedReality.Toolkit.Extensions.Experimental.SpectatorView.Utilities;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Experimental.SpectatorView
{
    public class MarkerVisualDetectorSpatialLocalizer : SpatialLocalizer
    {
        [Tooltip("The reference to an IMarkerDetector GameObject")]
        [SerializeField]
        private MonoBehaviour MarkerDetector = null;
        private IMarkerDetector markerDetector = null;

        protected override ISpatialCoordinateService SpatialCoordinateService => markerDetectorCoordinateService;
        private MarkerDetectorCoordinateService markerDetectorCoordinateService = null;
        private TaskCompletionSource<int> markerAssigned = null;
        private CancellationTokenSource discoveryCTS = null;

        private Dictionary<Guid, Action<Action<BinaryWriter>>> sessionWriteAndSendDictionary = new Dictionary<Guid, Action<Action<BinaryWriter>>>();
        private Dictionary<Guid, string> sessionCoordinateIdDictionary = new Dictionary<Guid, string>();
        private Dictionary<string, ISpatialCoordinate> coordinateDictionary = new Dictionary<string, ISpatialCoordinate>();
        private HashSet<string> neededCoordinates = new HashSet<string>();
        private bool localizing = false;

#if UNITY_EDITOR
        private void OnValidate()
        {
            FieldHelper.ValidateType<IMarkerDetector>(MarkerDetector);
        }
#endif

        private void Awake()
        {
            DebugLog("Awake", Guid.Empty);
            markerDetector = MarkerDetector as IMarkerDetector;
            if (markerDetector == null)
            {
                Debug.LogWarning("Marker detector not appropriately set for MarkerDetectorSpatialLocalizer");
            }

            markerDetectorCoordinateService = new MarkerDetectorCoordinateService(markerDetector, debugLogging);
            markerDetectorCoordinateService.CoordinatedDiscovered += OnCoordinateDiscovered;
        }

        private void OnDestroy()
        {
            markerDetectorCoordinateService.CoordinatedDiscovered -= OnCoordinateDiscovered;
        }

        private void Update()
        {
            if (localizing)
            {
                List<string> idsFound = null;
                foreach (var id in neededCoordinates)
                {
                    if (coordinateDictionary.ContainsKey(id))
                    {
                        if (idsFound == null)
                        {
                            idsFound = new List<string>();
                        }

                        idsFound.Add(id);
                    }
                }

                foreach(var id in idsFound)
                {
                    DebugLog($"Coordinate discovered, clearing from set: {id}", Guid.Empty);
                    neededCoordinates.Remove(id);
                }

                if (neededCoordinates.Count == 0)
                {
                    DebugLog("All coordinates found, ending discover", Guid.Empty);
                    discoveryCTS?.Cancel();
                    localizing = false;
                }
            }
        }

        internal override Task<Guid> InitializeAsync(bool actAsHost, CancellationToken cancellationToken)
        {
            Guid token = Guid.NewGuid();
            return Task.FromResult(token);
        }

        internal async override Task<ISpatialCoordinate> LocalizeAsync(bool actAsHost, Guid token, Action<Action<BinaryWriter>> writeAndSendMessage, CancellationToken cancellationToken)
        {
            sessionWriteAndSendDictionary[token] = writeAndSendMessage;

            // TODO - this means that if multiple coordinates are being detected at the same time for different spectators, theres no way to return known coordinates until all have been found?
            if (!localizing)
            {
                DebugLog($"Starting coordinate discovery", token);
                localizing = true;
                discoveryCTS = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                await markerDetectorCoordinateService.TryDiscoverCoordinatesAsync(discoveryCTS.Token);
            }
            else
            {
                DebugLog($"Coordinate discovery in process, waiting for completion", token);
                await Task.WhenAny(Task.Delay(-1, discoveryCTS.Token), Task.Delay(-1, cancellationToken));
            }

            if (sessionCoordinateIdDictionary.TryGetValue(token, out var id) &&
                coordinateDictionary.TryGetValue(id, out var coordinate))
            {
                DebugLog($"Coordinate discovery completed and coordinate was located", token);
                return coordinate;
            }

            DebugLog($"Coordinate discovery completed but coordinate wasn't located", token);
            return null;
        }

        internal override void ProcessIncomingMessage(bool actAsHost, Guid token, string command, BinaryReader r)
        {
            switch (command)
            {
                case MarkerVisualSpatialLocalizer.MarkerVisualDiscoveryHeader:
                    var maxId = r.Read();

                    string id = string.Empty;
                    for(int i = 0; i <= maxId; i++)
                    {
                        if (!neededCoordinates.Contains(i.ToString()))
                        {
                            id = i.ToString();
                        }
                    }

                    if (string.IsNullOrEmpty(id))
                    {
                        Debug.LogWarning("Failed to obtain an marker id for peer.");
                    }
                    else
                    {
                        DebugLog($"Assigning coordinate id: {id}", token);
                        neededCoordinates.Add(id);
                        sessionCoordinateIdDictionary.Add(token, id);

                        if (sessionWriteAndSendDictionary.TryGetValue(token, out var writeAndSendMessage))
                        {
                            SendCoordinateAssigned(writeAndSendMessage, id);
                        }
                        else
                        {
                            DebugLog("Failed to send assigned coordinate id, spatial localization may not work correctly.", token);
                        }
                    }
                    break;
                default:
                    DebugLog($"Unknown command observed: {command}", token);
                    break;
            }
        }

        internal override void Uninitialize(bool actAsHost, Guid token)
        {
        }

        private void OnCoordinateDiscovered(ISpatialCoordinate obj)
        {
            DebugLog($"Coordinate was discovered: {obj.Id}", Guid.Empty);
            // Tell participant peers what coordinates were detected
            coordinateDictionary.Add(obj.Id, obj);

            List<Guid> sessions = new List<Guid>();
            foreach (var sessionCoordinateIdPair in sessionCoordinateIdDictionary)
            {
                var token = sessionCoordinateIdPair.Key;
                var coordinateId = sessionCoordinateIdPair.Value;
                if (coordinateId == obj.Id)
                {
                    sessions.Add(token);
                }
            }

            foreach (var token in sessions)
            {
                if (sessionWriteAndSendDictionary.TryGetValue(token, out var writeAndSendMessage))
                {
                    DebugLog($"Sending that the coordinate was found: {obj.Id}", token);
                    SendCoordinateFound(writeAndSendMessage, obj.Id);
                }
                else
                {
                    DebugLog($"Write and send message not known.", token);
                }
            }
        }

        private void SendCoordinateAssigned(Action<Action<BinaryWriter>> writeAndSendMessage, string coordinateId)
        {
            writeAndSendMessage(writer =>
            {
                writer.Write(MarkerVisualSpatialLocalizer.CoordinateAssignedHeader);
                writer.Write(coordinateId);
            });
        }

        private void SendCoordinateFound(Action<Action<BinaryWriter>> writeAndSendMessage, string coordinateId)
        {
            writeAndSendMessage(writer =>
            {
                writer.Write(MarkerVisualSpatialLocalizer.CoordinateFoundHeader);
                writer.Write(coordinateId);
            });
        }
    }
}
