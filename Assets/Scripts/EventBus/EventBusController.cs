using System;
using UnityEngine;

namespace Rosynant.EventBus
{
    /// <summary>
    /// Resets all <see cref="EventBus{T}"/> instances on domain reload to prevent stale listener references
    /// between Play Mode sessions in the Editor. Uses reflection to discover all <see cref="IEvent"/> implementations
    /// automatically, so no manual registration is needed when new event types are added.
    /// </summary>
    public static class EventBusController
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAll()
        {
            var eventBusType = typeof(EventBus<>);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(IEvent).IsAssignableFrom(type) && (type.IsClass || type.IsValueType))
                    {
                        eventBusType.MakeGenericType(type).GetMethod(nameof(EventBus<IEvent>.Reset))?.Invoke(null, null);
                    }
                }
            }
        }
    }
}
