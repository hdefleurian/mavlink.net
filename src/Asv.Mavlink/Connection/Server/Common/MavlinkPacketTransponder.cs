﻿using System;
using System.Reactive.Linq;
using System.Threading;
using Asv.Mavlink.V2.Common;
using Nito.AsyncEx;
using NLog;

namespace Asv.Mavlink.Server
{
    public class MavlinkPacketTransponder<TPacket,TPayload> : IMavlinkPacketTransponder<TPacket,TPayload>
        where TPacket : IPacketV2<TPayload>, new()
        where TPayload : IPayload, new()
    {
        private readonly IMavlinkV2Connection _connection;
        private readonly MavlinkServerIdentity _identityConfig;
        private readonly IPacketSequenceCalculator _seq;
        private readonly object _sync = new object();
        private IDisposable _timerSubscribe;
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly AsyncReaderWriterLock _dataLock = new AsyncReaderWriterLock();
        private CancellationTokenSource _disposeCancellation = new CancellationTokenSource();
        private int _isSending;
        private readonly byte[] _payloadContent;
        private readonly RxValue<PacketTransponderState> _state = new RxValue<PacketTransponderState>();
        private int _payloadSize;
        private TPacket _packet;

        public MavlinkPacketTransponder(IMavlinkV2Connection connection, MavlinkServerIdentity identityConfig, IPacketSequenceCalculator seq)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (identityConfig == null) throw new ArgumentNullException(nameof(identityConfig));
            if (seq == null) throw new ArgumentNullException(nameof(seq));
            _connection = connection;
            _identityConfig = identityConfig;
            _seq = seq;
            _payloadContent = new byte[new TPacket().Payload.GetMaxByteSize()+1];
            _payloadSize = new TPacket().Payload.GetMaxByteSize();
        }

        public void Start(TimeSpan rate)
        {
            lock (_sync)
            {
                if (IsStarted)
                {
                    _timerSubscribe?.Dispose();
                    _timerSubscribe = null;
                }

                IsStarted = true;
                _timerSubscribe = Observable.Timer(TimeSpan.FromMilliseconds(1), rate).Subscribe(OnTick);
            }
        }

        private async void OnTick(long l)
        {
            if (Interlocked.CompareExchange(ref _isSending, 1, 0) == 1)
            {
                LogSkipped();
                return;
            }

            IDisposable dispose = null;
            try
            {

                dispose = await _dataLock.ReaderLockAsync();
                await _connection.Send((IPacketV2<IPayload>) _packet, _disposeCancellation.Token);
                LogSuccess();
            }
            catch (Exception e)
            {
                LogError(e);
               
            }
            finally
            {
                dispose?.Dispose();
                Interlocked.Exchange(ref _isSending, 0);
            }
        }

        private void LogError(Exception e)
        {
            if (_state.Value == PacketTransponderState.ErrorToSend) return;
            _state.OnNext(PacketTransponderState.ErrorToSend);
            _logger.Error(e, $"{new TPacket().Name} sending error:{e.Message}");
        }

        private void LogSuccess()
        {
            if (_state.Value == PacketTransponderState.Ok) return;
            _state.OnNext(PacketTransponderState.Ok);
            _logger.Debug($"{new TPacket().Name} start stream");
        }

        private void LogSkipped()
        {
            if (_state.Value == PacketTransponderState.Skipped) return;
            _state.OnNext(PacketTransponderState.Skipped);
            _logger.Warn($"{new TPacket().Name} skipped sending: previous command has not yet been executed");
        }

        public bool IsStarted { get; private set; }

        public IRxValue<PacketTransponderState> State => _state;

        public void Stop()
        {
            lock (_sync)
            {
                _timerSubscribe?.Dispose();
                _timerSubscribe = null;
                IsStarted = false;
            }
        }

        public void Set(Action<TPayload> changeCallback)
        {
            IDisposable locker = null;
            try
            {
                locker = _dataLock.WriterLock();
                _packet = new TPacket
                {
                    CompatFlags = 0,
                    IncompatFlags = 0,
                    Sequence = _seq.GetNextSequenceNumber(),
                    ComponenId = _identityConfig.ComponenId,
                    SystemId = _identityConfig.SystemId,
                };
                changeCallback(_packet.Payload);

            }
            catch (Exception e)
            {
                _logger.Error(e, $"Error to set new value for {new TPacket().Name}:{e.Message}");
            }
            finally
            {
                locker?.Dispose();
            }
        }

        public void Dispose()
        {
            Stop();
            _state?.Dispose();
            _disposeCancellation?.Cancel(false);
            _disposeCancellation?.Dispose();
            _disposeCancellation = null;
        }
    }
}