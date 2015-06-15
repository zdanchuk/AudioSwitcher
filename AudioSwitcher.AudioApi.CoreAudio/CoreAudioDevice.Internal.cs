﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AudioSwitcher.AudioApi.CoreAudio.Interfaces;
using AudioSwitcher.AudioApi.CoreAudio.Threading;

namespace AudioSwitcher.AudioApi.CoreAudio
{
    public sealed partial class CoreAudioDevice
    {
        private AudioMeterInformation _audioMeterInformation;
        private AudioEndpointVolume _audioEndpointVolume;
        private IDeviceTopology _deviceTopology;

        private IPropertyDictionary Properties
        {
            get
            {
                return _properties;
            }
        }

        /// <summary>
        /// Audio Meter Information - Future support
        /// </summary>
        private AudioMeterInformation AudioMeterInformation
        {
            get
            {
                return _audioMeterInformation;
            }
        }
        /// <summary>
        /// Audio Endpoint Volume
        /// </summary>
        private AudioEndpointVolume AudioEndpointVolume
        {
            get
            {
                return _audioEndpointVolume;
            }
        }

        private void GetPropertyInformation(IMMDevice device)
        {
            ComThread.Assert();

            if (_properties == null)
                _properties = new CachedPropertyDictionary();

            //Don't try to load properties for a device that doesn't exist
            if (State == DeviceState.NotPresent)
                return;
            
            _properties.TryLoadFrom(device);
        }

        private void LoadAudioMeterInformation(IMMDevice device)
        {
            //This should be all on the COM thread to avoid any
            //weird lookups on the result COM object not on an STA Thread
            ComThread.Assert();

            object result = null;
            Exception ex;
            //Need to catch here, as there is a chance that unauthorized is thrown.
            //It's not an HR exception, but bubbles up through the .net call stack
            try
            {
                var clsGuid = new Guid(ComInterfaceIds.AUDIO_METER_INFORMATION_IID);
                ex = Marshal.GetExceptionForHR(device.Activate(ref clsGuid, ClassContext.Inproc, IntPtr.Zero, out result));
            }
            catch (Exception e)
            {
                ex = e;
            }

            if (ex != null)
            {
                ClearAudioMeterInformation();
                return;
            }

            _audioMeterInformation = new AudioMeterInformation(result as IAudioMeterInformation);
        }

        private void LoadSpeakerConfig(IMMDevice device)
        {
            //This should be all on the COM thread to avoid any
            //weird lookups on the result COM object not on an STA Thread
            ComThread.Assert();

            try
            {

                if (_deviceTopology != null)
                    Marshal.FinalReleaseComObject(_deviceTopology);
            }
            catch
            {
                
            }

            Exception ex;



            //Need to catch here, as there is a chance that unauthorized is thrown.
            //It's not an HR exception, but bubbles up through the .net call stack
            try
            {

                var clsGuid = new Guid(ComInterfaceIds.DEVICE_TOPOLOGY_IID);
                object result;
                ex = Marshal.GetExceptionForHR(device.Activate(ref clsGuid, ClassContext.Inproc, IntPtr.Zero, out result));

                _deviceTopology = result as IDeviceTopology;

                if (_deviceTopology == null)
                    return;

                IConnector connector;
                _deviceTopology.GetConnector(0, out connector);

                // If the connector is not connected return.
                bool isConnected;
                connector.IsConnected(out isConnected);
                if (!isConnected) throw new Exception();

                var part = connector as IPart;

                var asfdasfd = FindParts(part);

                clsGuid = new Guid(ComInterfaceIds.AUDIO_CHANNEL_CONFIG_IID);

                foreach (var p in asfdasfd)
                {
                    p.Activate(ClassContext.Inproc, ref clsGuid, out result);
                    var speaker = result as IAudioChannelConfig;

                    if (result != null)
                    {
                        SpeakerConfiguration config;
                        speaker.GetChannelConfig(out config);
                    }
                }


            }
            catch (Exception e)
            {
                ex = e;
            }

            if (ex != null)
            {
                return;
            }
        }

        internal static List<IPart> FindParts(IPart part)
        {
            List<IPart> allParts = new List<IPart>();
            allParts.Add(part);

            PartType pType;
            part.GetPartType(out pType);

            if (pType == PartType.Connector)
            {
                var connector = (IConnector) part;

                // If the connector is not connected return.
                bool isConnected;
                connector.IsConnected(out isConnected);
                if (!isConnected) return allParts;

                // Otherwise, get the other connector.
                IConnector connectedTo;
                connector.GetConnectedTo(out connectedTo);
                IPart connectedPart = (IPart) connectedTo;

                // Then enumerate all outgoing parts.
                IPartsList partsOutgoing;
                connectedPart.EnumPartsIncoming(out partsOutgoing);
                if (partsOutgoing == null) return allParts;

                // If there are more outgoing parts, get each one.
                UInt32 partCount;
                partsOutgoing.GetCount(out partCount);

                for (uint i = 0; i < partCount; i++)
                {
                    IPart nextPart;
                    partsOutgoing.GetPart(i, out nextPart);
                    allParts.AddRange(FindParts(nextPart));
                }

            }
            else if (pType == PartType.Subunit)
            {
                // Get sub type, for debugging purposes.
                Guid subType;
                part.GetSubType(out subType);

                // Just enumerate all outgoing parts.
                IPartsList partsOutgoing;
                part.EnumPartsIncoming(out partsOutgoing);
                if (partsOutgoing == null) return allParts;

                // If there are more outgoing parts, get each one.
                UInt32 partCount;
                partsOutgoing.GetCount(out partCount);

                for (uint i = 0; i < partCount; i++)
                {
                    IPart nextPart;
                    partsOutgoing.GetPart(i, out nextPart);
                    allParts.AddRange(FindParts(nextPart));
                }
            }

            return allParts;
        }

        private void LoadAudioEndpointVolume(IMMDevice device)
        {
            //Don't even bother looking up volume for disconnected devices
            if (!State.HasFlag(DeviceState.Active))
            {
                ClearAudioEndpointVolume();
                return;
            }

            //This should be all on the COM thread to avoid any
            //weird lookups on the result COM object not on an STA Thread
            ComThread.Assert();

            object result = null;
            Exception ex;
            //Need to catch here, as there is a chance that unauthorized is thrown.
            //It's not an HR exception, but bubbles up through the .net call stack
            try
            {
                var clsGuid = new Guid(ComInterfaceIds.AUDIO_ENDPOINT_VOLUME_IID);
                ex = Marshal.GetExceptionForHR(device.Activate(ref clsGuid, ClassContext.Inproc, IntPtr.Zero, out result));
            }
            catch (Exception e)
            {
                ex = e;
            }

            if (ex != null)
            {
                ClearAudioEndpointVolume();
                return;
            }

            _audioEndpointVolume = new AudioEndpointVolume(result as IAudioEndpointVolume);
        }

        private void ClearAudioEndpointVolume()
        {
            if (_audioEndpointVolume != null)
            {
                _audioEndpointVolume.OnVolumeNotification -= AudioEndpointVolume_OnVolumeNotification;
                _audioEndpointVolume.Dispose();
                _audioEndpointVolume = null;
            }
        }

        private void ClearAudioMeterInformation()
        {
            if (_audioMeterInformation != null)
            {
                _audioMeterInformation.Dispose();
                _audioMeterInformation = null;
            }
        }

        private static readonly Dictionary<string, DeviceIcon> IconMap = new Dictionary<string, DeviceIcon>
        {
            {"0", DeviceIcon.Speakers},
            {"1", DeviceIcon.Speakers},
            {"2", DeviceIcon.Headphones},
            {"3", DeviceIcon.LineIn},
            {"4", DeviceIcon.Digital},
            {"5", DeviceIcon.DesktopMicrophone},
            {"6", DeviceIcon.Headset},
            {"7", DeviceIcon.Phone},
            {"8", DeviceIcon.Monitor},
            {"9", DeviceIcon.StereoMix},
            {"10", DeviceIcon.Speakers},
            {"11", DeviceIcon.Kinect},
            {"12", DeviceIcon.DesktopMicrophone},
            {"13", DeviceIcon.Speakers},
            {"14", DeviceIcon.Headphones},
            {"15", DeviceIcon.Speakers},
            {"16", DeviceIcon.Headphones},
            {"3004", DeviceIcon.Speakers},
            {"3010", DeviceIcon.Speakers},
            {"3011", DeviceIcon.Headphones},
            {"3012", DeviceIcon.LineIn},
            {"3013", DeviceIcon.Digital},
            {"3014", DeviceIcon.DesktopMicrophone},
            {"3015", DeviceIcon.Headset},
            {"3016", DeviceIcon.Phone},
            {"3017", DeviceIcon.Monitor},
            {"3018", DeviceIcon.StereoMix},
            {"3019", DeviceIcon.Speakers},
            {"3020", DeviceIcon.Kinect},
            {"3021", DeviceIcon.DesktopMicrophone},
            {"3030", DeviceIcon.Speakers},
            {"3031", DeviceIcon.Headphones},
            {"3050", DeviceIcon.Speakers},
            {"3051", DeviceIcon.Headphones},
        };

        private static DeviceIcon IconStringToDeviceIcon(string iconStr)
        {
            try
            {
                string imageKey = iconStr.Substring(iconStr.IndexOf(",", StringComparison.InvariantCultureIgnoreCase) + 1).Replace("-", "");
                return IconMap[imageKey];
            }
            catch
            {
                return DeviceIcon.Unknown;
            }
        }
    }
}
