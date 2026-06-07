using UnityEngine;
using UnityEngine.UI;

namespace Rosynant.EventBus.Sample
{
    public class EventBusSubscriberSample : MonoBehaviour, IEventSubscriber
    {
        [SerializeField]
        private Image _image;

        public EventSubscriptionBag EventSubscriptionBag { get; } = new();

        private void Start()
        {
            EventBusProvider.Subscribe<TestEvent>(OnTestEvent).AddToSubscriptionBag(EventSubscriptionBag);
        }

        private void OnTestEvent(TestEvent testEvent)
        {
            _image.color = testEvent.Color;
        }

        private void OnDestroy()
        {
            EventSubscriptionBag.Dispose();
        }
    }
}