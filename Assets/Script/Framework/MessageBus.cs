using System;
using System.Collections.Generic;

namespace PirateCaptain.Framework
{
    public interface IMessageBus
    {
        IDisposable Subscribe<T>(Action<T> handler);
        void Publish<T>(T message);
    }

    /// <summary>在 Unity 主线程使用。按订阅顺序同步派发，回调中新增的订阅从下次发布起生效。</summary>
    public sealed class MessageBus : IMessageBus
    {
        private readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>();

        public IDisposable Subscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var subscription = new Subscription<T>(this, handler);
            _handlers.TryGetValue(typeof(T), out var previous);
            _handlers[typeof(T)] = Delegate.Combine(previous, new Action<T>(subscription.Invoke));
            return subscription;
        }

        public void Publish<T>(T message)
        {
            if (_handlers.TryGetValue(typeof(T), out var handlers))
                ((Action<T>)handlers)(message);
        }

        private void Remove<T>(Action<T> handler)
        {
            var remaining = Delegate.Remove(_handlers[typeof(T)], handler);
            if (remaining == null) _handlers.Remove(typeof(T));
            else _handlers[typeof(T)] = remaining;
        }

        private sealed class Subscription<T> : IDisposable
        {
            private MessageBus _owner;
            private Action<T> _handler;

            public Subscription(MessageBus owner, Action<T> handler)
            {
                _owner = owner;
                _handler = handler;
            }

            public void Invoke(T message) => _handler?.Invoke(message);

            public void Dispose()
            {
                if (_owner == null) return;
                _owner.Remove<T>(Invoke);
                _handler = null;
                _owner = null;
            }
        }
    }
}
