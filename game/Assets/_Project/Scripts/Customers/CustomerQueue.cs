using System;
using System.Collections.Generic;
using Tycoon.Config;
using Tycoon.Core;
using UnityEngine;

namespace Tycoon.Customers
{
    /// <summary>
    /// A shelf a supermarket shopper can browse: the buffer they take from, and where they
    /// stand to do it.
    /// </summary>
    [Serializable]
    public class StoreShelf
    {
        public Tycoon.Stations.ItemBuffer buffer;
        public Transform standPoint;

        [Tooltip("Optional. Waypoints from the shop's hub out to the stand, for a shop whose " +
                 "shelves are not all in reach of a straight line from one another (free-standing " +
                 "rows). Shoppers steer in straight lines and cannot see shelves, so these are " +
                 "what keep them in the aisles. Shelves share nodes by sharing the same " +
                 "Transform, which makes the whole set a tree: walking from one shelf to the next " +
                 "backs out only as far as the nodes they do not share. Empty means a direct line.")]
        public Transform[] via;

        [Tooltip("Optional. The supply crate behind this shelf. A shelf whose crate has not been " +
                 "ordered yet is bought but not trading, so shoppers do not ask for its goods.")]
        public GameObject openWhen;

        /// <summary>
        /// Open for shoppers: the shelf exists (shelves are saved inactive until bought) and,
        /// if it has a crate to wait for, that crate has been ordered.
        /// </summary>
        public bool IsOpen =>
            buffer != null && buffer.item != null && buffer.gameObject.activeInHierarchy &&
            (openWhen == null || openWhen.activeInHierarchy);

        /// <summary>
        /// Adds the positions a shopper must walk to get from the end of <paramref name="trail"/>
        /// (the nodes it has walked out along, hub first) to the end of <paramref name="via"/>,
        /// and leaves <paramref name="trail"/> describing where it is afterwards. Backs out of
        /// the nodes the two do not share, passes through the last node they DO share (the
        /// junction, which is a real place on the floor the shopper has to walk to), then walks
        /// in along the rest. The one place this is worked out: the shopper uses it to walk, and
        /// the layout audit to check the walk.
        /// </summary>
        public static void Connect(List<Transform> trail, Transform[] via, List<Vector3> into)
        {
            int length = via != null ? via.Length : 0;
            int shared = 0;
            while (shared < trail.Count && shared < length && trail[shared] == via[shared]) shared++;

            for (int i = trail.Count - 1; i >= shared; i--)
                if (trail[i] != null) into.Add(trail[i].position);
            trail.RemoveRange(shared, trail.Count - shared);

            // The junction: the last node the two share, which is also the end of the walk when
            // the destination is further up the same aisle. Not when the shopper has not entered
            // the tree yet (nothing shared) or is backing all the way out (nothing to walk in to).
            if (shared > 0 && trail[shared - 1] != null)
                into.Add(trail[shared - 1].position);

            for (int i = shared; i < length; i++)
            {
                if (via[i] == null) continue;
                into.Add(via[i].position);
                trail.Add(via[i]);
            }
        }

        /// <summary>Backs all the way out to the hub.</summary>
        public static void Unwind(List<Transform> trail, List<Vector3> into) => Connect(trail, null, into);
    }

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

        [Tooltip("Seconds between arrivals at full reputation, with a single unit of " +
                 "production behind the counter. Demand and reputation both divide into it.")]
        public float spawnIntervalSeconds = 2.5f;

        [Tooltip("Extra demand per unit of production capacity past the first. At 0.35 a " +
                 "second coop brings shoppers about a third faster, and each extra hen " +
                 "counts too - the farm growing is felt at the till without retuning anything.")]
        public float demandPerCapacity = 0.35f;

        [Tooltip("Ceiling on demand growth. The queue only has so many standing slots, so " +
                 "past this point a faster spawn rate just churns shoppers who cannot fit.")]
        public float maxDemand = 3f;

        [Header("Second checkout (supermarket only)")]
        [Tooltip("Other tills a finished shopper may queue at instead of this one. They choose " +
                 "the shortest queue, nearest on a tie. Only listed on the queue that spawns the " +
                 "shoppers; an alternate has no shelves and spawns nobody. A till that has not " +
                 "been bought yet is inactive and is simply not offered.")]
        public CustomerQueue[] alternates;

        [Tooltip("Set on an alternate till: reputation belongs to the shop, not the till, so both " +
                 "queues move the same number and the price multiplier is the same at both.")]
        public CustomerQueue sharesReputationWith;

        [Header("Demand by product line (supermarket only)")]
        [Tooltip("Product lines (open shelves) the base spawn interval is tuned for.")]
        [Min(1)] public int baselineLines = 3;

        [Tooltip("Extra arrival rate per open line past the baseline: 0.25 is a quarter faster " +
                 "for each product the shop has added.")]
        public float demandPerExtraLine = 0.25f;

        [Tooltip("Ceiling on that growth, as a multiple of the baseline rate.")]
        public float maxLineDemand = 2f;

        [Header("Browsing (supermarket only)")]
        [Tooltip("Leave empty for a farm till. When set, this queue spawns shoppers who first " +
                 "walk to these shelves, take goods from them, and only then queue at the " +
                 "checkout - the shelves are the menu, and `register` is not used.")]
        public StoreShelf[] shelves;

        [Tooltip("Where a shopper walks on the way in, after the road and before the first shelf.")]
        public Transform[] entryRoute;

        [Tooltip("Where a shopper walks on the way out, after the checkout and before the exit.")]
        public Transform[] exitRoute;

        [Tooltip("Where a shopper walks after the last shelf and before joining the queue, so " +
                 "the straight line from a shelf to the queue never cuts across the counter.")]
        public Transform[] queueApproach;

        [Tooltip("Waypoints from this till's queue to the shared way out. Only someone leaving "
               + "the queue walks them; a shopper giving up in the aisles goes straight out.")]
        public Transform[] queueExit;

        [Tooltip("Most shoppers in the shop at once, browsing and queueing together.")]
        [Min(1)] public int maxShoppers = 5;

        [Min(1)] public int maxProducts = 2;
        [Min(1)] public int maxUnitsPerProduct = 3;

        [Tooltip("Reputation lost each time a shopper gives up on an item because the shelf " +
                 "stayed empty. Smaller than a walkout: they still bought the rest.")]
        public float reputationPerSkip = 0.04f;

        [Header("Reputation")]
        [Range(0f, 1f)] public float startingReputation = 1f;
        public float reputationPerHappy = 0.05f;
        public float reputationPerAngry = 0.14f;
        [Range(0f, 1f)] public float minReputation = 0.25f;

        private readonly List<CustomerAgent> _waiting = new List<CustomerAgent>();
        private readonly List<CustomerAgent> _browsing = new List<CustomerAgent>();
        private float _spawnTimer;
        private float _reputation = -1f;

        public float Reputation =>
            sharesReputationWith != null ? sharesReputationWith.Reputation : _reputation;

        /// <summary>True for a supermarket queue, whose shoppers browse before they pay.</summary>
        public bool IsBrowsing => shelves != null && shelves.Length > 0;

        /// <summary>What the shop can charge. A neglected counter genuinely earns less.</summary>
        public float PriceMultiplier => Mathf.Lerp(0.6f, 1.15f, Mathf.Clamp01(Reputation));

        /// <summary>
        /// How much busier this counter is than a one-hen farm.
        ///
        /// Read live off the production registry rather than stored, so unlocking a coop
        /// or buying a cow moves it immediately with nothing to notify. The same
        /// mechanism covers the dairy till: it sells milk, so it scales with cows.
        /// </summary>
        public float DemandMultiplier
        {
            get
            {
                var menu = register != null ? register.sells : null;
                if (menu == null) return 1f;

                int capacity = 0;
                for (int i = 0; i < menu.Length; i++)
                    capacity += ProductRegistry.Capacity(menu[i]);

                return Mathf.Clamp(1f + demandPerCapacity * Mathf.Max(0, capacity - 1),
                                   1f, Mathf.Max(1f, maxDemand));
            }
        }

        /// <summary>
        /// How much busier a supermarket is than at its baseline number of product lines: one
        /// quarter more for each open line past the baseline, capped. Read live off the shelves
        /// so buying a shelf and ordering its goods is felt at the till with nothing to notify.
        /// </summary>
        public float LineDemand
        {
            get
            {
                int open = 0;
                if (shelves != null)
                    for (int i = 0; i < shelves.Length; i++)
                        if (shelves[i] != null && shelves[i].IsOpen) open++;

                float demand = 1f + demandPerExtraLine * Mathf.Max(0, open - baselineLines);
                return Mathf.Clamp(demand, 1f, Mathf.Max(1f, maxLineDemand));
            }
        }

        /// <summary>Everyone in the shop: browsing, plus waiting at this till or any other.</summary>
        private int TotalShoppers
        {
            get
            {
                int total = _waiting.Count + _browsing.Count;
                if (alternates != null)
                    foreach (var other in alternates)
                        if (other != null) total += other._waiting.Count;
                return total;
            }
        }

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
            _browsing.RemoveAll(c => c == null);

            if (customerTemplate == null || slots == null || slots.Length == 0) return;

            if (IsBrowsing)
            {
                UpdateBrowsingSpawn();
                return;
            }

            if (register == null || register.sells == null || register.sells.Length == 0) return;

            if (_waiting.Count >= slots.Length) return;

            _spawnTimer += Mathf.Min(Time.deltaTime, 0.1f);

            // A poorly run shop sees fewer people through the door; a bigger farm sees more.
            // The slot limit above is still the hard cap on how many can be waiting at once,
            // so this only changes how quickly the queue refills.
            float interval = spawnIntervalSeconds /
                             (DemandMultiplier * Mathf.Max(0.2f, _reputation));
            if (_spawnTimer < interval) return;

            _spawnTimer = 0f;
            Spawn();
        }

        /// <summary>
        /// The supermarket's version of the spawn clock. Same reputation scaling as the farm;
        /// the limit is the number of people in the shop rather than the number of queue slots,
        /// because most of them are off browsing and not queueing.
        /// </summary>
        private void UpdateBrowsingSpawn()
        {
            if (TotalShoppers >= maxShoppers) return;

            _spawnTimer += Mathf.Min(Time.deltaTime, 0.1f);

            float interval = spawnIntervalSeconds / (Mathf.Max(0.2f, Reputation) * LineDemand);
            if (_spawnTimer < interval) return;

            _spawnTimer = 0f;
            SpawnShopper();
        }

        private void SpawnShopper()
        {
            Vector3 origin = spawnPoint != null ? spawnPoint.position : transform.position;

            var go = Instantiate(customerTemplate, origin, Quaternion.identity, transform);
            go.name = "Shopper";
            go.SetActive(true);

            var agent = go.GetComponent<CustomerAgent>();
            if (agent == null)
            {
                Destroy(go);
                return;
            }

            // A list of one or two products, one to three of each. Deliberately NOT limited to
            // what is on the shelf right now: an empty shelf is the pressure on the player, and
            // a shopper who only ever asked for what was already there would never feel it.
            // Only lines that are open: a shelf bought but not yet supplied is not trading.
            var order = new List<int>();
            for (int i = 0; i < shelves.Length; i++)
                if (shelves[i] != null && shelves[i].IsOpen)
                    order.Add(i);

            if (order.Count == 0)
            {
                Destroy(go);
                return;
            }

            int products = Mathf.Min(UnityEngine.Random.Range(1, maxProducts + 1), order.Count);
            var stops = new List<ShoppingStop>();
            for (int n = 0; n < products; n++)
            {
                int pick = UnityEngine.Random.Range(0, order.Count);
                var shelf = shelves[order[pick]];
                order.RemoveAt(pick);

                stops.Add(new ShoppingStop
                {
                    shelf = shelf,
                    item = shelf.buffer.item,
                    wanted = UnityEngine.Random.Range(1, maxUnitsPerProduct + 1)
                });
            }

            // In the order the shelves are listed, which the level lays out as a tour of the
            // shop, so a shopper does not zig-zag between aisles.
            stops.Sort((a, b) => System.Array.IndexOf(shelves, a.shelf)
                .CompareTo(System.Array.IndexOf(shelves, b.shelf)));

            var route = new List<Vector3>();
            if (entryRoute != null)
                foreach (var point in entryRoute)
                    if (point != null) route.Add(point.position);

            _browsing.Add(agent);
            agent.BeginShopping(this, stops, route, RandomTint());
        }

        /// <summary>
        /// A browsing shopper has finished and wants to queue. Succeeds only if a place is free;
        /// otherwise they wait where they are and ask again.
        /// </summary>
        public bool TryJoinQueue(CustomerAgent customer, out Vector3 slot, out CustomerQueue joined)
        {
            slot = default;
            joined = null;

            // The shortest queue wins; on a tie, the one whose next place is nearer the shopper.
            // Alternates that are not bought yet are inactive and not offered.
            CustomerQueue best = null;
            float bestDistance = 0f;
            Consider(this, customer, ref best, ref bestDistance);
            if (alternates != null)
                foreach (var other in alternates)
                    if (other != null && other.isActiveAndEnabled)
                        Consider(other, customer, ref best, ref bestDistance);

            if (best == null) return false;

            _browsing.Remove(customer);
            best._waiting.Add(customer);
            slot = best.SlotPosition(best._waiting.Count - 1);
            joined = best;
            return true;
        }

        private static void Consider(CustomerQueue candidate, CustomerAgent customer,
            ref CustomerQueue best, ref float bestDistance)
        {
            if (candidate.slots == null || candidate.slots.Length == 0) return;
            if (candidate._waiting.Count >= candidate.slots.Length) return;

            float distance = (candidate.SlotPosition(candidate._waiting.Count) -
                              customer.transform.position).sqrMagnitude;

            bool better = best == null ||
                          candidate._waiting.Count < best._waiting.Count ||
                          (candidate._waiting.Count == best._waiting.Count && distance < bestDistance);
            if (!better) return;

            best = candidate;
            bestDistance = distance;
        }

        /// <summary>A shopper gave up on an item because its shelf stayed empty.</summary>
        public void OnItemSkipped() => AdjustReputation(-reputationPerSkip);

        /// <summary>Adds the approach to the queue, in order, to a finished shopper's route.</summary>
        public void AppendQueueApproach(List<Vector3> into)
        {
            if (queueApproach == null) return;
            foreach (var point in queueApproach)
                if (point != null) into.Add(point.position);
        }

        /// <summary>Adds the way from this till's queue to the shared exit route.</summary>
        public void AppendQueueExit(List<Vector3> into)
        {
            if (queueExit == null) return;
            foreach (var point in queueExit)
                if (point != null) into.Add(point.position);
        }

        /// <summary>Adds the way out of the shop, in order, to a leaving shopper's route.</summary>
        public void AppendExitRoute(List<Vector3> into)
        {
            if (exitRoute == null) return;
            foreach (var point in exitRoute)
                if (point != null) into.Add(point.position);
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
            _browsing.Remove(customer);
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
            if (sharesReputationWith != null)
            {
                sharesReputationWith.AdjustReputation(delta);
                return;
            }

            _reputation = Mathf.Clamp(_reputation + delta, minReputation, 1f);
        }

        [Serializable]
        private struct State { public float reputation; }

        public string CaptureState() => JsonUtility.ToJson(new State { reputation = _reputation });

        public void RestoreState(string json)
        {
            // A second till keeps no reputation of its own; the shop's is restored by the first.
            if (sharesReputationWith != null) return;

            var s = JsonUtility.FromJson<State>(json);
            _reputation = Mathf.Clamp(s.reputation, minReputation, 1f);
        }
    }
}
