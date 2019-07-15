﻿using System;
using Asv.Mavlink.V2.Common;

namespace Asv.Mavlink.Client
{
    public interface ILoggingClient:IDisposable
    {
        IRxValue<LoggingDataPayload> RawLoggingData { get; }
    }
}