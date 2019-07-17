﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asv.Mavlink.V2.Common;
using NLog;

namespace Asv.Mavlink.Server
{
    public class StatusTextLoggerConfig
    {
        public int MaxQueueSize { get; set; } = 100;
        public int MaxSendRateHz { get; set; } = 10;
    }

    public class StatusTextServer : IDisposable, IStatusTextServer
    {
        private readonly int _maxMessageSize = new StatustextPayload().Text.Length;
        private readonly IMavlinkV2Connection _connection;
        private readonly IPacketSequenceCalculator _seq;
        private readonly MavlinkServerIdentity _identity;
        private readonly StatusTextLoggerConfig _config;
        private readonly ConcurrentQueue<KeyValuePair<MavSeverity,string>> _messageQueue = new ConcurrentQueue<KeyValuePair<MavSeverity, string>>();
        private readonly CancellationTokenSource _disposeCancel = new CancellationTokenSource();
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private int _isSending;

        public StatusTextServer(IMavlinkV2Connection connection,IPacketSequenceCalculator seq,MavlinkServerIdentity identity, StatusTextLoggerConfig config)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (seq == null) throw new ArgumentNullException(nameof(seq));
            if (config == null) throw new ArgumentNullException(nameof(config));
            
            _connection = connection;
            _seq = seq;
            _identity = identity;
            _config = config;

            _logger.Debug($"Create status logger for [sys:{identity.SystemId}, com:{identity.ComponenId}] with send rate:{config.MaxSendRateHz} Hz, buffer size: {config.MaxQueueSize}");

            Observable.Timer(TimeSpan.FromSeconds(1.0 / _config.MaxSendRateHz),
                TimeSpan.FromSeconds(1.0 / _config.MaxSendRateHz)).Subscribe(TrySend, _disposeCancel.Token);
        }

        private async void TrySend(long l)
        {
            if (Interlocked.CompareExchange(ref _isSending,1,0) == 1) return;

            try
            {
                KeyValuePair<MavSeverity, string> res;
                if (_messageQueue.TryDequeue(out res))
                {
                    await _connection.Send(new StatustextPacket
                    {
                        ComponenId = _identity.ComponenId,
                        SystemId = _identity.SystemId,
                        CompatFlags = 0,
                        IncompatFlags = 0,
                        Sequence = _seq.GetNextSequenceNumber(),
                        Payload =
                        {
                            Severity = res.Key,
                            Text = res.Value.ToCharArray()
                        }
                    }, _disposeCancel.Token);
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Error occured to send packet: {e.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _isSending, 0);
            }
        }

        public bool Log(MavSeverity severity, string message)
        {
            _logger.Trace($"=>{severity:G}:{message}");

            if (message.Length > _maxMessageSize)
            {
                _logger.Warn($"Message size ({message.Length} char) is more then we can send by packet (max size {_maxMessageSize}).");
                _logger.Warn($"Original: [{severity}]=>{message}");
                var newMessage = message.Substring(0, _maxMessageSize - 3) + "...";
                _logger.Warn($"Reduced: [{severity}]=>{newMessage}");
                return true;
            }
            if (_messageQueue.Count > _config.MaxQueueSize)
            {
                _logger.Warn($"Message queue overflow (current size:{_messageQueue.Count}).");
                return false;
            }
            _messageQueue.Enqueue(new KeyValuePair<MavSeverity, string>(severity,message));
            return true;
        }

        public void Dispose()
        {
            _disposeCancel?.Cancel(false);
            _disposeCancel?.Dispose();
        }
    }

    
}