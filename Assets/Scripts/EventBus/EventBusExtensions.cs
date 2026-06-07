using System;

namespace Rosynant.EventBus
{
    public static class EventBusExtensions
    {
        /// <summary>
        /// Adds this subscription handle to <paramref name="bag"/>.
        /// Allows fluent chaining: <c>EventBus&lt;T&gt;.Subscribe(cb).AddToSubscriptionBag(bag);</c>
        /// </summary>
        /// <param name="disposable">The handle returned by <see cref="EventBus{T}.Subscribe"/>.</param>
        /// <param name="bag">The bag that will dispose this handle in <c>OnDestroy</c>.</param>
        public static void AddToSubscriptionBag(this IDisposable disposable, EventSubscriptionBag bag)
        {
            bag.Add(disposable);
        }
    }
}