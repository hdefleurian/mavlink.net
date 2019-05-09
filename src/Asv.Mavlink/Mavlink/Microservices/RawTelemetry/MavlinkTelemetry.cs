﻿using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using Asv.Mavlink.V2.Common;

namespace Asv.Mavlink
{
    public class RawTelemetryConfig
    {
        public byte SystemId { get; set; } = 254;
        public byte ComponentId { get; set; } = 254;
        public byte TargetSystemId { get; } = 1;
        public byte TargetComponenId { get; } = 1;
        
    }

    public class MavlinkTelemetry : IMavlinkTelemetry,IDisposable
    {
        private readonly RawTelemetryConfig _config;
        private readonly RxValue<HeartbeatPayload> _heartBeat = new RxValue<HeartbeatPayload>();
        private readonly RxValue<SysStatusPayload> _sysStatus = new RxValue<SysStatusPayload>();
        private readonly RxValue<GpsRawIntPayload> _gpsRawInt = new RxValue<GpsRawIntPayload>();
        private readonly RxValue<HighresImuPayload> _highresImu = new RxValue<HighresImuPayload>();
        private readonly RxValue<VfrHudPayload> _vfrHud = new RxValue<VfrHudPayload>();
        private readonly RxValue<AttitudePayload> _attitude = new RxValue<AttitudePayload>();
        private readonly RxValue<BatteryStatusPayload> _batteryStatus = new RxValue<BatteryStatusPayload>();
        private readonly RxValue<AltitudePayload> _altitude = new RxValue<AltitudePayload>();
        private readonly RxValue<ExtendedSysStatePayload> _extendedSysState = new RxValue<ExtendedSysStatePayload>();
        private readonly RxValue<HomePositionPayload> _home = new RxValue<HomePositionPayload>();
        private readonly RxValue<StatustextPayload> _statusText = new RxValue<StatustextPayload>();
        private readonly RxValue<GlobalPositionIntPayload> _globalPositionInt = new RxValue<GlobalPositionIntPayload>();
        private readonly IObservable<IPacketV2<IPayload>> _inputPackets;
        private readonly CancellationTokenSource _disposeCancel = new CancellationTokenSource();

        private readonly RxValue<int> _packetRate = new RxValue<int>();

        public MavlinkTelemetry(IMavlinkV2Connection connection, RawTelemetryConfig config)
        {
            _config = config;
            _inputPackets = connection.Where(FilterVehicle);

            HandleStatistic();
            HandleHeartbeat(config);
            HandleSystemStatus();
            HandleGps();
            HandleHighresImu();
            HandleVfrHud();
            HandleAttitude();
            HandleBatteryStatus();
            HandleAltitude();
            HandleExtendedSysState();
            HandleHome();
            HandleGlobalPositionInt();
        }

       


        public IRxValue<int> PacketRateHz => _packetRate;
        public IRxValue<HeartbeatPayload> RawHeartbeat => _heartBeat;
        public IRxValue<SysStatusPayload> RawSysStatus => _sysStatus;
        public IRxValue<GpsRawIntPayload> RawGpsRawInt => _gpsRawInt;
        public IRxValue<HighresImuPayload> RawHighresImu => _highresImu;
        public IRxValue<ExtendedSysStatePayload> RawExtendedSysState => _extendedSysState;
        public IRxValue<AltitudePayload> RawAltitude => _altitude;
        public IRxValue<BatteryStatusPayload> RawBatteryStatus => _batteryStatus;
        public IRxValue<AttitudePayload> RawAttitude => _attitude;
        public IRxValue<VfrHudPayload> RawVfrHud => _vfrHud;
        public IRxValue<HomePositionPayload> RawHome => _home;
        public IRxValue<StatustextPayload> RawStatusText => _statusText;
        public IRxValue<GlobalPositionIntPayload> RawGlobalPositionInt => _globalPositionInt;

        private void HandleGlobalPositionInt()
        {
            _inputPackets
                .Where(_ => _.MessageId == GlobalPositionIntPacket.PacketMessageId)
                .Cast<GlobalPositionIntPacket>()
                .Select(_ => _.Payload)
                .Subscribe(_globalPositionInt, _disposeCancel.Token);
            _disposeCancel.Token.Register(() => _globalPositionInt.Dispose());

        }

        private void HandleHome()
        {
            _inputPackets
                .Where(_ => _.MessageId == HomePositionPacket.PacketMessageId)
                .Cast<HomePositionPacket>()
                .Select(_ => _.Payload)
                .Subscribe(_=>_home.OnNext(_), _disposeCancel.Token);
           

            _disposeCancel.Token.Register(() => _home.Dispose());
        }

        private void HandleExtendedSysState()
        {
            _inputPackets
                .Where(_ => _.MessageId == ExtendedSysStatePacket.PacketMessageId)
                .Cast<ExtendedSysStatePacket>()
                .Select(_ => _.Payload)
                .Subscribe(_extendedSysState, _disposeCancel.Token);
            _disposeCancel.Token.Register(() => _extendedSysState.Dispose());
        }

        private void HandleAltitude()
        {
            _inputPackets
                .Where(_ => _.MessageId == AltitudePacket.PacketMessageId)
                .Cast<AltitudePacket>()
                .Select(_ => _.Payload)
                .Subscribe(_altitude, _disposeCancel.Token);
            _disposeCancel.Token.Register(() => _altitude.Dispose());
        }

        private void HandleBatteryStatus()
        {
            _inputPackets
                .Where(_ => _.MessageId == BatteryStatusPacket.PacketMessageId)
                .Cast<BatteryStatusPacket>()
                .Select(_ => _.Payload)
                .Subscribe(_batteryStatus, _disposeCancel.Token);
            _disposeCancel.Token.Register(() => _batteryStatus.Dispose());
        }

        private void HandleAttitude()
        {
            _inputPackets
                .Where(_ => _.MessageId == AttitudePacket.PacketMessageId)
                .Cast<AttitudePacket>()
                .Select(_ => _.Payload)
                .Subscribe(_attitude, _disposeCancel.Token);
            _disposeCancel.Token.Register(() => _attitude.Dispose());
        }

        private void HandleVfrHud()
        {
            _inputPackets
                .Where(_ => _.MessageId == VfrHudPacket.PacketMessageId)
                .Cast<VfrHudPacket>()
                .Select(_ => _.Payload)
                .Subscribe(_vfrHud, _disposeCancel.Token);
            _disposeCancel.Token.Register(() => _vfrHud.Dispose());
        }

        private void HandleHighresImu()
        {
            _inputPackets
                .Where(_ => _.MessageId == HighresImuPacket.PacketMessageId)
                .Cast<HighresImuPacket>()
                .Select(_ => _.Payload)
                .Subscribe(_highresImu, _disposeCancel.Token);
            _disposeCancel.Token.Register(() => _highresImu.Dispose());
        }

        private void HandleGps()
        {
            var s = _inputPackets
                .Where(_ => _.MessageId == GpsRawIntPacket.PacketMessageId)
                .Cast<GpsRawIntPacket>()
                .Select(_ => _.Payload);
            s.Subscribe(_gpsRawInt, _disposeCancel.Token);
            _disposeCancel.Token.Register(() => _gpsRawInt.Dispose());
        }

        private void HandleSystemStatus()
        {
            _inputPackets
                .Where(_ => _.MessageId == SysStatusPacket.PacketMessageId)
                .Cast<SysStatusPacket>()
                .Select(_ => _.Payload)
                .Subscribe(_sysStatus, _disposeCancel.Token);
            _inputPackets
                .Where(_ => _.MessageId == StatustextPacket.PacketMessageId)
                .Cast<StatustextPacket>()
                .Select(_ => _.Payload)
                .Subscribe(_statusText, _disposeCancel.Token);

            _disposeCancel.Token.Register(() => _sysStatus.Dispose());
            _disposeCancel.Token.Register(() => _statusText.Dispose());
        }


       

        private void HandleStatistic()
        {
            _inputPackets
                .Select(_ => 1)
                .Buffer(TimeSpan.FromSeconds(1))
                .Select(_ => _.Sum()).Subscribe(_packetRate, _disposeCancel.Token);
            _disposeCancel.Token.Register(() => _packetRate.Dispose());
        }

        private void HandleHeartbeat(RawTelemetryConfig config)
        {
            _inputPackets
                .Where(_ => _.MessageId == HeartbeatPacket.PacketMessageId)
                .Cast<HeartbeatPacket>()
                .Select(_=>_.Payload)
                .Subscribe(_heartBeat, _disposeCancel.Token);
            _disposeCancel.Token.Register(() => _heartBeat.Dispose());
        }



        private bool FilterVehicle(IPacketV2<IPayload> packetV2)
        {
            if (_config.TargetSystemId != 0 && _config.TargetSystemId != packetV2.SystemId) return false;
            if (_config.TargetComponenId != 0 && _config.TargetComponenId != packetV2.ComponenId) return false;
            return true;
        }

        public void Dispose()
        {
            _disposeCancel.Cancel(false);
            _disposeCancel.Dispose();
        }
    }
}