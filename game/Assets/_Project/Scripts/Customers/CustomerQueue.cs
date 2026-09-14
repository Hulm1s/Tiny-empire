using System;
using System.Collections.Generic;
using Tycoon.Config;
using Tycoon.Core;
using UnityEngine;

namespace Tycoon.Customers
{
    /// <summary>
    /// Spawns shoppers on the road, walks them to the counter, keeps the queue in order and
    /// tracks how well the shop is being run.
    ///
    /// Reputation is the fourth upkeep pressure: customers who give up and walk out drag it
    /// down, which both thins the queue and cuts the price everything sells for. A shop left
    /// unattended does not simply stop earning, it gets worse - and it recovers only by being
    /// served properly again.
    /// </summary>
    public class CustomerQueue : MonoBehaviour, ISaveable
    {
        [Header("Spawning")]
        [Tooltip("Inactive customer object in the scene, cloned for each shopper.")]
        public GameObject customerTemplate;

        [Tooltip("Where shoppers walk in from, on the road.")]
        public Transform spawnPoint;

        [Tooltip("Where they walk off to once done.")]
        public Transform exitPoint;

        [Tooltip("The counter they face while waiting.")]
        public Transform counterPoint;

        [Tooltip("Standing positions, nearest the counter first.")]
        public Transform[] slots;

        [Header("Orders")]
        [Tooltip("The till these shoppers queue at. Its `sells` list is the menu: a shopper is " +
                 "only ever created for something this counter can actually hand over.")]
        public Tycoon.Stations.RegisterStation register;

        [Min(1)] public int minOrder = 1;
        [Min(1)] public int maxOrder = 4;

        [Tooltip("Seconds between arrivals at full reputation. A poor shop gets fewer.")]
        public float spawnIntervalSeconds = 5f;

        [Header("Reputation")]
        [Range(0f, 1f)] public float startingReputation = 1f;
        public float reputationPerHappy = 0.05f;
        public float reputationPerAngry = 0.14f;
        [Range(0f, 1f)] public float minReputation = 0.25f;

        private readonly List<CustomerAgent> _waiting = new List<CustomerAgent>();
        private float _spawnTimer;
        private float _reputation = -1f;

        public float Reputation => _reputation;

        /// <summary>What the shop can charge. A neglected counter genuinely earns less.</summary>
        public float PriceMultiplier => Mathf.Lerp(0.6f, 1.15f, Mathf.Clamp01(_reputation));

        public Vector3 ExitPosition => exitPoint != null ? exitPoint.position : transform.position;
        public Vector3 CounterPosition => counterPoint != null ? counterPoint.position : transform.position;

        public string SaveKey => SaveKeys.For(this);

        /// <summary>The shopper currently at the head of the queue, if they are ready to be served.</summary>
        public CustomerAgent Front
        {
            get
            {
                for (int i = 0; i < _waiting.Count; i++)
                {
                    var customer = _waiting[i];
                    if (customer == null) continue;
                    if (customer.IsWaiting && !customer.IsSatisfied) return customer;
                    // Only the head of the queue can be served; if they are still walking in,
                    // nobody behind them gets served early.
                    return null;
                }
                return null;
            }
        }

        private void Awake()
        {
            if (_reputation < 0f) _reputation = startingReputation;
        }

        private void OnEnable() => SaveSystem.Register(this);
        private void OnDisable() => SaveSystem.Unregister(this);

        private void Update()
        {
            _waiting.RemoveAll(c => c == null);

            if (customerTemplate == null || slots == null || slots.Length == 0) return;
            if (register == null || register.sells == null || register.sells.Length == 0) return;

            if (_waiting.Count >= slots.Length) return;

            _spawnTimer += Mathf.Min(Time.deltaTime, 0.1f);

            // A poorly run shop sees fewer people through the door.
            float interval = spawnIntervalSeconds / Mathf.Max(0.2f, _reputation);
            if (_spawnTimer < interval) return;

            _spawnTimer = 0f;
            Spawn();
        }

        private void Spawn()
        {
            Vector3 origin = spawnPoint != null ? spawnPoint.position : transform.position;

            var go = Instantiate(customerTemplate, origin, Quaternion.identity, transform);
            go.name = "Customer";
            go.SetActive(true);

            var agent = go.GetComponent<CustomerAgent>();
            if (agent == null)
            {
                Destroy(go);
                return;
            }

            var item = PickOrderableItem();

            // Two separate reasons to turn a shopper away at the door, and both matter:
            // nothing on this till's menu can be made yet, or - if a level is ever wired up
            // wrongly - the order does not match what this till sells. Either way, never send
            // somebody in to queue for something that can never be handed to them.
            if (item == null || register == null || !register.CanFulfill(item))
            {
                Destroy(go);
                return;
            }

            int count = UnityEngine.Random.Range(minOrder, maxOrder + 1);

            _waiting.Add(agent);
            agent.Begin(this, item, count, SlotPosition(_waiting.Count - 1), RandomTint());
        }

        /// <summary>
        /// A random item from this till's menu that the farm can currently produce, or null.
        ///
        /// Two filters, doing different jobs. The register's own list decides what this counter
        /// is for at all; <see cref="ProductRegistry"/> then holds back anything the player has
        /// no way of making yet, so a dairy till stands empty until the first cow arrives rather
        /// than queueing up orders that could only ever time out.
        /// </summary>
        private ItemDefinition PickOrderableItem()
        {
            var menu = register != null ? register.sells : null;
            if (menu == null) return null;

            int available = 0;
            for (int i = 0; i < menu.Length; i++)
                if (ProductRegistry.CanProduce(menu[i])) available++;

            if (available == 0) return null;

            int pick = UnityEngine.Random.Range(0, available);
            for (int i = 0; i < menu.Length; i++)
            {
                if (!ProductRegistry.CanProduce(menu[i])) continue;
                if (pick-- == 0) return menu[i];
            }

            return null;
        }

        private static Color RandomTint()
        {
            // Pleasant, well-separated hues so a queue of shoppers reads as different people.
            float hue = UnityEngine.Random.value;
            return Color.HSVToRGB(hue, 0.45f, 0.92f);
        }

        private Vector3 SlotPosition(int index)
        {
            if (slots == null || slots.Length == 0) return transform.position;
            int clamped = Mathf.Clamp(index, 0, slots.Length - 1);
            return slots[clamped] != null ? slots[clamped].position : transform.position;
        }

        public void OnCustomerLeft(CustomerAgent customer, bool happy)
        {
            int index = _waiting.IndexOf(customer);
            if (index >= 0) _waiting.RemoveAt(index);

            AdjustReputation(happy ? reputationPerHappy : -reputationPerAngry);

            // Everybody behind shuffles up one place.
            for (int i = 0; i < _waiting.Count; i++)
            {
                if (_waiting[i] != null) _waiting[i].MoveToSlot(SlotPosition(i));
            }
        }

        private void AdjustReputation(float delta)
        {
            _reputation = Mathf.Clamp(_reputation + delta, minReputation, 1f);
        }

        [Serializable]
        private struct State { public float reputation; }

        public string CaptureState() => JsonUtility.ToJson(new State { reputation = _reputation });

        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<State>(json);
            _reputation = Mathf.Clamp(s.reputation, minReputation, 1f);
        }
    }
}
