using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rosynant.EventBus
{
    /// <summary>
    /// Lazily-instantiated, <c>DontDestroyOnLoad</c> singleton that owns and caches one <see cref="EventBus{T}"/>
    /// per event type, and is the sole entry point for subscribing/broadcasting.
    /// <para>
    /// Use the static <see cref="Subscribe{T}"/> / <see cref="Broadcast{T}"/> methods — do not reach for
    /// <see cref="Instance"/> directly (it's internal precisely to steer callers towards those, see its
    /// own remarks for why).
    /// </para>
    /// <para>
    /// Tying bus lifetime to this singleton's own Unity lifecycle (<see cref="OnDestroy"/>) is what lets it
    /// replace the previous static-generic design's separate, reflection-based <c>EventBusController</c>:
    /// the buses now reset themselves as a natural consequence of the singleton being torn down, rather than
    /// needing an external hook to discover and reset every closed <c>EventBus&lt;T&gt;</c>.
    /// </para>
    /// </summary>
    public class EventBusProvider : MonoBehaviour
    {
        private static EventBusProvider _instance;

        private Dictionary<Type, IEventBus> _eventBusCache = new();

        // Sticky for the remainder of the runtime session once shutdown begins (see OnDestroy/OnApplicationQuit),
        // so that any late call to Instance during teardown returns a real null instead of resurrecting a brand
        // new singleton GameObject moments before everything is destroyed anyway — the classic Unity
        // "ghost singleton on quit" pitfall. Reset at the start of every session by ResetLifecycleState.
        private static bool _isEndingLifecycle;

        /// <summary>
        /// The lazily-created singleton, or a genuine <c>null</c> if the provider is unavailable (shutting down).
        /// </summary>
        /// <remarks>
        /// The safe way to consume this is through
        /// <see cref="Subscribe{T}"/>/<see cref="Broadcast{T}"/>, which null-check it once using Unity's
        /// overloaded <c>==</c> idiom.
        /// </remarks>
        internal static EventBusProvider Instance
        {
            get
            {
                if (_instance == null) // Unity's overloaded == also reports true for a destroyed-but-referenced object.
                {
                    // Collapse that "fake null" into a real C# null. Without this, _instance could keep pointing at
                    // a destroyed component, and the `return null;` below — or any other == null check — would be
                    // comparing against that stale reference rather than a clean slate.
                    _instance = null;

                    if (_isEndingLifecycle)
                    {
                        return null; // Refuse to resurrect a new singleton once the app/domain is tearing down.
                    }

                    _instance = new GameObject(nameof(EventBusProvider)).AddComponent<EventBusProvider>();
                    DontDestroyOnLoad(_instance.gameObject); // Survives scene loads; destroyed only on domain reload / app quit.
                }
                return _instance;
            }
        }

        /// <summary>
        /// Subscribes <paramref name="callback"/> to events of type <typeparamref name="T"/> via the singleton
        /// provider. Returns <c>null</c> if the provider is unavailable (e.g. mid-shutdown) — safe to pass
        /// straight into <see cref="EventBusExtensions.AddToSubscriptionBag"/>, which guards against null handles.
        /// </summary>
        public static IDisposable Subscribe<T>(Action<T> callback) where T : IEvent
        {
            // Captured once: re-reading the property would re-run its creation/teardown branching a second time
            // and risks the null-check and the subsequent dereference observing different states.
            EventBusProvider instance = Instance;

            if (instance == null)
            {
                return null;
            }

            return instance.ProcessSubscription(callback);
        }

        /// <summary>
        /// Broadcasts <paramref name="event"/> to all subscribers of type <typeparamref name="T"/> via the
        /// singleton provider. No-ops if the provider is unavailable (e.g. mid-shutdown).
        /// </summary>
        public static void Broadcast<T>(T @event) where T : IEvent
        {
            EventBusProvider instance = Instance;

            if (instance == null)
            {
                return;
            }

            instance.ProcessBroadcast(@event);
        }

        // Instance-side halves of Subscribe/Broadcast, kept private: the "provider might be unavailable" handling
        // belongs solely to the static entry points above, so there's exactly one place that can get it wrong —
        // not a second, instance-level path that some future caller could invoke directly and bypass it.
        private IDisposable ProcessSubscription<T>(Action<T> callback) where T : IEvent
        {
            EventBus<T> eventBus = GetEventBus<T>();
            return eventBus.Subscribe(callback);
        }

        private void ProcessBroadcast<T>(T @event) where T : IEvent
        {
            EventBus<T> eventBus = GetEventBus<T>();
            eventBus.Broadcast(@event);
        }

        /// <summary>
        /// Clears the static lifecycle flags at the start of every runtime session — Editor Play Mode entry or
        /// app launch — regardless of whether a domain reload happened.
        /// </summary>
        /// <remarks>
        /// This is what breaks an otherwise-unrecoverable deadlock: if "Reload Domain" is disabled in
        /// Enter Play Mode Settings, <see cref="_isEndingLifecycle"/> and <see cref="_instance"/> would carry
        /// their end-of-session values into the next session. <see cref="Instance"/> would then see
        /// <c>_isEndingLifecycle == true</c> and refuse to create a new singleton — but the only code that resets
        /// that flag runs as a side effect of successfully creating one. Neither field could ever recover on its
        /// own. Hooking <c>SubsystemRegistration</c> sidesteps that entirely by resetting both unconditionally
        /// before anything else in the session can touch <see cref="Instance"/>.
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetLifecycleState()
        {
            _isEndingLifecycle = false;
            _instance = null;
        }

        /// <summary>Returns the cached <see cref="EventBus{T}"/> for <typeparamref name="T"/>, creating and caching one on first use.</summary>
        private EventBus<T> GetEventBus<T>() where T : IEvent
        {
            EventBus<T> eventBus;

            if (_eventBusCache.TryGetValue(typeof(T), out IEventBus cachedEventBus))
            {
                // Safe: this dictionary only ever stores an EventBus<T> under the key typeof(T).
                eventBus = cachedEventBus as EventBus<T>;
            }
            else
            {
                eventBus = new EventBus<T>();
                _eventBusCache.Add(typeof(T), eventBus);
            }

            return eventBus;
        }

        private void OnDestroy()
        {
            _isEndingLifecycle = true;

            foreach (var eventBus in _eventBusCache.Values)
            {
                eventBus.Reset();
            }
            _eventBusCache.Clear();
        }

        // Marks the lifecycle as ending slightly ahead of OnDestroy in the quit sequence, closing the small window
        // where other objects' own shutdown code could still call into Instance and resurrect a fresh singleton
        // moments before the application finishes tearing everything down.
        private void OnApplicationQuit()
        {
            _isEndingLifecycle = true;
        }
    }
}
