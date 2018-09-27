﻿using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Asv.Mavlink
{
    public class RxValue<TValue> :IObserver<TValue>, IRxValue<TValue>,IDisposable
    {
        private readonly Subject<TValue> _subject = new Subject<TValue>();
        private TValue _value;

        public TValue Value => _value;

        public void Dispose()
        {
            _subject.Dispose();
        }

        public void OnNext(TValue value)
        {
            _value = value;
            _subject.OnNext(value);
        }

        public void OnError(Exception error)
        {
            _subject.OnError(error);
        }

        public void OnCompleted()
        {
            _subject.OnCompleted();
        }

        public IDisposable Subscribe(IObserver<TValue> observer)
        {
            return _subject.DistinctUntilChanged().Subscribe(observer);
        }
    }
}