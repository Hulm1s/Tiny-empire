using System;
using System.Collections.Generic;
using System.Text;
using Tycoon.Core;
using Tycoon.Player;
using Tycoon.Stations;
using Tycoon.Upkeep;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tycoon.Tasks
{
    public enum TaskKind { Tutorial, Problem, Goal }

    /// <summary>One line of the task list, and the square the arrow points at for it.</summary>
    public struct TaskItem
    {
        public TaskKind kind;
        public string label;
        public StationBase target;

        /// <summary>Drawn faded: the thing coming after the one to do now.</summary>
        public bool dim;
    }

    /// <summary>
    /// What the player should be doing next, worked out a few times a second.
    ///
    /// Three kinds of line, in the order they matter:
    /// <list type="bullet">
    /// <item><b>Tutorial</b> (green): the first loop, until <see cref="TutorialProgress"/> is done.</item>
    /// <item><b>Problems</b> (red): something that has stopped or is about to - a jam, an empty
    /// feed trough, a full basket, an empty shelf. Read off <see cref="AlertBeacon.Active"/>.</item>
    /// <item><b>Goals</b> (yellow): the cheapest purchase or step on offer right now.</item>
    /// </list>
    ///
    /// The board only reads. It holds no state of its own beyond which list the arrow follows,
    /// so a wrong line can never damage a save, and every line is re-derived from the world on
    /// the next sweep - buy the thing and the goal moves on by itself.
    ///
    /// Built by <see cref="Tycoon.UI.HudRoot"/>. Read by <see cref="Tycoon.UI.TaskPanel"/> and
    /// <see cref="GuideArrow"/>.
    /// </summary>
    public class TaskBoard : MonoBehaviour
    {
        public static TaskBoard Instance { get; private set; }

        /// <summary>Most lines the panel shows at once.</summary>
        public const int MaxLines = 3;

        /// <summary>Seconds between sweeps. These are slow conditions; never per frame.</summary>
        private const float Interval = 0.25f;

        private const string ArrowPref = "tycoon.arrow";

        public readonly List<TaskItem> Tutorial = new List<TaskItem>(MaxLines);
        public readonly List<TaskItem> Problems = new List<TaskItem>(MaxLines);
        public readonly List<TaskItem> Goals = new List<TaskItem>(MaxLines);

        /// <summary>How many problems there are in all, not just the ones with a line.</summary>
        public int ProblemCount { get; private set; }

        public bool InTutorial { get; private set; }

        /// <summary>Which step the tutorial is on, 0-based. Meaningful only while <see cref="InTutorial"/>.</summary>
        public int TutorialStep { get; private set; }

        /// <summary>The list the arrow follows, and the panel shows.</summary>
        public TaskKind Followed { get; private set; } = TaskKind.Goal;

        /// <summary>The list <see cref="Followed"/> names.</summary>
        public List<TaskItem> Shown =>
            Followed == TaskKind.Tutorial ? Tutorial : Followed == TaskKind.Problem ? Problems : Goals;

        /// <summary>
        /// Where the arrow should go: the top line's square, or the travel square that leads to
        /// it when it is in the other location. Null when there is nothing to point at.
        /// </summary>
        public StationBase ArrowTarget { get; private set; }

        /// <summary>Fired after every sweep, for the panel to redraw.</summary>
        public event Action Rebuilt;

        private bool _wantsProblems;
        private float _timer;

        // Scene objects are found once and reused. Everything is looked up including inactive
        // ones, because a locked purchase's building starts switched off - the stations that
        // matter are checked for being live on every sweep.
        private bool _cached;
        private UnlockStation[] _unlocks;
        private UpgradeStation[] _upgrades;
        private ChoreStation[] _chores;
        private PaintStation[] _paints;
        private CollectStation[] _collects;
        private DepositStation[] _deposits;
        private RegisterStation[] _registers;
        private CutStation[] _benches;
        private FlowerBed[] _beds;
        private WorkerAgent[] _workers;
        private DiscardStation[] _bins;
        private AtmStation[] _atms;
        private CashCollectStation[] _tillCash;
        private TravelStation[] _travels;
        private Location[] _locations;

        private Transform _player;
        private CarryStack _carry;

        private struct Found
        {
            public int severity;
            public float distance;
            public TaskItem item;
        }

        private readonly List<Found> _found = new List<Found>(8);
        private readonly StringBuilder _text = new StringBuilder(48);

        private void Awake()
        {
            Instance = this;
            _wantsProblems = PlayerPrefs.GetInt(ArrowPref, 0) == 1;
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // A wipe reloads the scene, and every reference above is to the old one.
            _cached = false;
            _player = null;
            _carry = null;
            _timer = 0f;
        }

        private void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;

            _timer = Interval;
            Rebuild();
        }

        /// <summary>Switches the arrow between the problem list and the goal list.</summary>
        public void SetFollow(TaskKind kind)
        {
            if (kind == TaskKind.Tutorial) return;

            _wantsProblems = kind == TaskKind.Problem;
            PlayerPrefs.SetInt(ArrowPref, _wantsProblems ? 1 : 0);
            PlayerPrefs.Save();
            Rebuild();
        }

        // ---- the sweep -------------------------------------------------------------------

        private void Rebuild()
        {
            EnsureCache();
            EnsurePlayer();

            Tutorial.Clear();
            Problems.Clear();
            Goals.Clear();

            var progress = TutorialProgress.Instance;
            InTutorial = progress != null && !progress.IsDone;
            TutorialStep = progress != null ? progress.Step : 0;

            if (InTutorial) BuildTutorial(progress);
            BuildProblems();
            BuildGoals();

            Followed = InTutorial ? TaskKind.Tutorial : _wantsProblems ? TaskKind.Problem : TaskKind.Goal;

            var shown = Shown;
            ArrowTarget = shown.Count > 0 ? ResolveForArrow(shown[0].target) : null;

            Rebuilt?.Invoke();
        }

        private void EnsureCache()
        {
            if (_cached) return;
            _cached = true;

            const FindObjectsInactive inactive = FindObjectsInactive.Include;
            const FindObjectsSortMode none = FindObjectsSortMode.None;
            _unlocks = FindObjectsByType<UnlockStation>(inactive, none);
            _upgrades = FindObjectsByType<UpgradeStation>(inactive, none);
            _chores = FindObjectsByType<ChoreStation>(inactive, none);
            _paints = FindObjectsByType<PaintStation>(inactive, none);
            _collects = FindObjectsByType<CollectStation>(inactive, none);
            _deposits = FindObjectsByType<DepositStation>(inactive, none);
            _registers = FindObjectsByType<RegisterStation>(inactive, none);
            _benches = FindObjectsByType<CutStation>(inactive, none);
            _beds = FindObjectsByType<FlowerBed>(inactive, none);
            _workers = FindObjectsByType<WorkerAgent>(inactive, none);
            _bins = FindObjectsByType<DiscardStation>(inactive, none);
            _atms = FindObjectsByType<AtmStation>(inactive, none);
            _tillCash = FindObjectsByType<CashCollectStation>(inactive, none);
            _travels = FindObjectsByType<TravelStation>(inactive, none);
            _locations = FindObjectsByType<Location>(inactive, none);
        }

        private void EnsurePlayer()
        {
            if (_player != null) return;

            var go = GameObject.FindWithTag("Player");
            if (go == null) return;

            _player = go.transform;
            _carry = go.GetComponent<CarryStack>();
        }

        private static bool Live(Component c) => c != null && c.gameObject.activeInHierarchy;

        private float DistanceToPlayer(Component c)
        {
            if (_player == null || c == null) return 0f;
            return (c.transform.position - _player.position).sqrMagnitude;
        }

        // ---- tutorial --------------------------------------------------------------------

        private static readonly string[] StepLabels =
        {
            "Harvest some corn",
            "Feed the chickens",
            "Collect the eggs",
            "Sell eggs at the counter",
            "Throw something in a bin",
        };

        private void BuildTutorial(TutorialProgress p)
        {
            int last = Mathf.Min(p.Step + 1, TutorialProgress.StepCount - 1);
            for (int step = p.Step; step <= last; step++)
            {
                string label = StepLabels[step];
                StationBase target = TutorialTarget(p, step, ref label);
                Tutorial.Add(new TaskItem
                {
                    kind = TaskKind.Tutorial,
                    label = label,
                    target = target,
                    dim = step != p.Step
                });
            }
        }

        /// <summary>
        /// The square for a step. Where the step needs something in the player's arms and they
        /// are not holding it, this points at where to get it instead - so a player who threw
        /// the corn away, or sold the last egg, is never left pointed at a square that cannot work.
        /// </summary>
        private StationBase TutorialTarget(TutorialProgress p, int step, ref string label)
        {
            switch (step)
            {
                case 0:
                    return p.field;

                case 1:
                {
                    var corn = p.feed != null && p.feed.target != null ? p.feed.target.item : null;
                    if (_carry != null && corn != null && !_carry.Has(corn))
                    {
                        label = "Harvest corn first";
                        return p.field;
                    }
                    return p.feed;
                }

                case 2:
                    return p.collect;

                case 3:
                {
                    var egg = p.collect != null && p.collect.source != null ? p.collect.source.item : null;
                    if (_carry != null && egg != null && !_carry.Has(egg))
                    {
                        label = "Collect eggs to sell";
                        return p.collect;
                    }
                    return p.till;
                }

                default:
                {
                    if (_carry != null && _carry.IsEmpty)
                    {
                        label = "Pick up anything to bin";
                        return p.field;
                    }
                    return NearestBin();
                }
            }
        }

        private StationBase NearestBin()
        {
            DiscardStation best = null;
            float bestD = float.MaxValue;
            var here = PlayerLocation();

            foreach (var bin in _bins)
            {
                if (!Live(bin)) continue;
                if (here != null && bin.GetComponentInParent<Location>() != here) continue;

                float d = DistanceToPlayer(bin);
                if (d < bestD) { bestD = d; best = bin; }
            }
            return best;
        }

        // ---- problems --------------------------------------------------------------------

        private void BuildProblems()
        {
            _found.Clear();

            var beacons = AlertBeacon.Active;
            for (int i = 0; i < beacons.Count; i++)
            {
                var b = beacons[i];
                if (b == null) continue;

                b.Refresh();
                string name = Pretty(b.businessName);

                if (b.IsJammed)
                    AddProblem(0, $"Fix {name} - jammed", b.repairSquare);
                else if (b.IsWearing)
                    AddProblem(1, $"Fix {name} - worn", b.repairSquare);

                if (b.NeedsFeed)
                    AddProblem(2, $"Feed {name}", b.feedSquare);

                if (b.OutputIsFull)
                {
                    var item = b.outputBuffer != null ? b.outputBuffer.item : null;
                    AddProblem(3, item != null ? $"Collect {item.displayName} at {name}" : $"Collect at {name}",
                        b.collectSquare);
                }
            }

            // The shop has no beacons: its shelves are the buffers, and an empty one is a
            // shopper turned away. Only shelves that are switched on count.
            for (int i = 0; i < _deposits.Length; i++)
            {
                var stock = _deposits[i];
                if (!Live(stock) || stock.Icon != Tycoon.UI.SquareIcon.Stock) continue;
                if (stock.target == null || stock.target.item == null || !stock.target.IsEmpty) continue;

                AddProblem(4, $"Restock {stock.target.item.displayName}", stock);
            }

            // The butcher's counter: each case that is switched on and bare is a shopper turned away.
            for (int i = 0; i < _registers.Length; i++)
            {
                var counter = _registers[i];
                if (!Live(counter) || !counter.HasDisplay) continue;
                // The florist's cases are filled by the beds, never by hand: a bare one is a bed to tend (below).
                if (counter is BouquetCounter) continue;

                foreach (var shelf in counter.stock)
                {
                    if (shelf == null || !Live(shelf) || shelf.item == null || !shelf.IsEmpty) continue;
                    AddProblem(4, $"Restock {shelf.item.displayName}", counter);
                }
            }

            // The florist's beds: plant, water, pick. One line per flower and job, at the nearest bed that
            // wants it: pointing at where to get the seed or the water first, and at the bed itself once
            // it is in the player's arms. A gardener on the payroll does all of this, so no lines then.
            if (!GardenerWorksBeds())
            {
                _bedBest.Clear();
                _bedBestDistance.Clear();
                for (int i = 0; i < _beds.Length; i++)
                {
                    var bed = _beds[i];
                    if (!Live(bed) || bed.Flower == null || bed.Job == 0) continue;

                    int key = bed.Flower.GetInstanceID() * 4 + bed.Job;
                    float d = DistanceToPlayer(bed.square);
                    if (_bedBest.ContainsKey(key) && _bedBestDistance[key] <= d) continue;
                    _bedBest[key] = bed;
                    _bedBestDistance[key] = d;
                }

                foreach (var pair in _bedBest)
                {
                    var bed = pair.Value;
                    string flower = bed.Flower.displayName.ToLowerInvariant() + "s";
                    switch (bed.Job)
                    {
                        case 1:
                        {
                            bool holding = _carry != null && _carry.Has(bed.seed);
                            var target = holding || !Live(bed.seedSquare) ? (StationBase)bed.square : bed.seedSquare;
                            AddProblem(3, holding ? $"Plant the {flower}" : $"Fetch seeds for the {flower}", target);
                            break;
                        }
                        case 2:
                        {
                            bool holding = _carry != null && _carry.Has(bed.water);
                            var target = holding || !Live(bed.wellSquare) ? (StationBase)bed.square : bed.wellSquare;
                            AddProblem(2, holding ? $"Water the {flower}" : $"Fetch water for the {flower}", target);
                            break;
                        }
                        default:
                            AddProblem(2, $"Harvest the {flower}", bed.square);
                            break;
                    }
                }
            }

            ProblemCount = _found.Count;

            _found.Sort((a, b) =>
            {
                int bySeverity = a.severity.CompareTo(b.severity);
                return bySeverity != 0 ? bySeverity : a.distance.CompareTo(b.distance);
            });

            for (int i = 0; i < _found.Count && i < MaxLines; i++) Problems.Add(_found[i].item);
        }

        private readonly Dictionary<int, FlowerBed> _bedBest = new Dictionary<int, FlowerBed>();
        private readonly Dictionary<int, float> _bedBestDistance = new Dictionary<int, float>();

        /// <summary>A hired gardener is on the beds (they plant, water and harvest by themselves).</summary>
        private bool GardenerWorksBeds()
        {
            for (int i = 0; i < _workers.Length; i++)
            {
                var w = _workers[i];
                if (Live(w) && w.routes != null && w.routes.Length > 0 && w.routes[0] != null && w.routes[0].dropoff is BedStation)
                    return true;
            }
            return false;
        }

        private void AddProblem(int severity, string label, StationBase target)
        {
            _found.Add(new Found
            {
                severity = severity,
                distance = DistanceToPlayer(target),
                item = new TaskItem { kind = TaskKind.Problem, label = label, target = target }
            });
        }

        /// <summary>"CowShedB" to "Cow Shed B": the builder's object names, made readable.</summary>
        private string Pretty(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            _text.Clear();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]) && name[i - 1] != ' ')
                    _text.Append(' ');
                _text.Append(name[i]);
            }
            return _text.ToString();
        }

        // ---- goals -----------------------------------------------------------------------

        private StationBase _best, _second;
        private double _bestPrice, _secondPrice;
        private float _bestDist, _secondDist;
        private string _bestLabel, _secondLabel;

        /// <summary>
        /// The two cheapest things on offer right now. "Cheapest first" is the order a player
        /// naturally buys in on this farm, and it needs no list to keep in step with the level.
        /// Free steps (carrying out the rubbish, opening the doors) come first for the same reason.
        /// </summary>
        private void BuildGoals()
        {
            _best = _second = null;
            _bestPrice = _secondPrice = double.MaxValue;
            _bestDist = _secondDist = float.MaxValue;
            _bestLabel = _secondLabel = null;
            _cheapest = double.MaxValue;

            for (int i = 0; i < _unlocks.Length; i++)
            {
                var u = _unlocks[i];
                if (!Live(u) || !u.enabled || u.IsUnlocked) continue;

                string verb = u.icon == Tycoon.UI.SquareIcon.Hire ? "Hire " : "Buy ";
                string label = u.icon == Tycoon.UI.SquareIcon.Fix
                    ? $"Repair the shop - {MoneyFormat.Short(u.Remaining)}"
                    : $"{verb}{u.label} - {MoneyFormat.Short(u.Remaining)}";
                Consider(u, u.price, label);
                _cheapest = Math.Min(_cheapest, u.Remaining);
            }

            for (int i = 0; i < _upgrades.Length; i++)
            {
                var u = _upgrades[i];
                if (!Live(u) || u.SoldOut) continue;

                Consider(u, u.CurrentPrice, $"Buy {u.unitName} - {MoneyFormat.Short(u.Remaining)}");
                _cheapest = Math.Min(_cheapest, u.Remaining);
            }

            for (int i = 0; i < _paints.Length; i++)
            {
                var p = _paints[i];
                if (!Live(p) || p.IsPainted) continue;

                Consider(p, p.firstPrice, $"Paint the shop - {MoneyFormat.Short(p.firstPrice)}");
                _cheapest = Math.Min(_cheapest, p.firstPrice);
            }

            for (int i = 0; i < _chores.Length; i++)
            {
                var c = _chores[i];
                if (!Live(c) || c.IsDone) continue;

                string label = c.icon == Tycoon.UI.SquareIcon.Open ? "Open the shop" : c.label;
                Consider(c, 0d, label);
            }

            // The shop's rubbish: the only goods with no value and no worker allowed near them.
            for (int i = 0; i < _collects.Length; i++)
            {
                var c = _collects[i];
                if (!Live(c) || c.workerCompatible || c.source == null || c.source.IsEmpty) continue;
                if (c.source.item == null || c.source.item.basePrice > 0d) continue;

                Consider(c, 0d, "Clear out the rubbish");
            }

            // The butcher's benches: a counter case that has run bare, with raw meat waiting, is a free job
            // for the player - "Grind sausages", "Cut steaks" - unless they are already holding the goods.
            for (int i = 0; i < _benches.Length; i++)
            {
                var bench = _benches[i];
                if (!Live(bench) || bench.output == null || bench.outputCase == null) continue;
                if (!Live(bench.outputCase) || !bench.outputCase.IsEmpty) continue;
                if (bench.meat == null || bench.meat.IsEmpty) continue;
                if (_carry != null && _carry.Has(bench.output)) continue;

                Consider(bench, 0d, bench.makes > 1 ? "Grind sausages" : "Cut steaks");
            }

            // The florist: a customer at the counter whose whole bouquet is in stock is a job for the
            // player (the bouquet minigame, with a tip) - a free step, like opening the shop.
            for (int i = 0; i < _registers.Length; i++)
            {
                if (!(_registers[i] is BouquetCounter florist) || !Live(florist)) continue;
                if (florist.ReadyCustomer == null || Staffed(florist)) continue;
                Consider(florist, 0d, "Make a bouquet");
            }

            AddCashGoals();

            if (_best != null)
                Goals.Add(new TaskItem { kind = TaskKind.Goal, label = _bestLabel, target = _best });
            if (_second != null)
                Goals.Add(new TaskItem { kind = TaskKind.Goal, label = _secondLabel, target = _second, dim = true });
        }

        private double _cheapest;

        /// <summary>A hired hand works this square (so it is not a job waiting for the player).</summary>
        private bool Staffed(StationBase station)
        {
            for (int i = 0; i < _workers.Length; i++)
                if (Live(_workers[i]) && _workers[i].dropoff == station) return true;
            return false;
        }

        /// <summary>
        /// Purchases are paid from the cash in hand, so a goal the player cannot cover from their
        /// pockets needs a step first: gather the takings waiting at a till, or draw on the account
        /// at the ATM. Both are free steps, so they sort ahead of the purchase they unlock.
        /// </summary>
        private void AddCashGoals()
        {
            if (_cheapest == double.MaxValue) return;

            var cashStack = CashStack.Instance;
            double inHand = cashStack != null ? cashStack.Amount : 0d;
            if (inHand >= _cheapest) return;

            CashCollectStation pile = null;
            float pileD = float.MaxValue;
            double waiting = 0d;
            for (int i = 0; i < _tillCash.Length; i++)
            {
                var c = _tillCash[i];
                if (!Live(c) || c.pile == null || c.pile.Amount < 1d) continue;
                waiting += c.pile.Amount;
                float d = DistanceToPlayer(c);
                if (d < pileD) { pileD = d; pile = c; }
            }

            if (pile != null)
                Consider(pile, 0d, "Collect cash at the till");

            var wallet = GameRoot.Money;
            double account = wallet != null ? wallet.Balance : 0d;
            if (account >= 1d && inHand + account >= _cheapest)
            {
                // The ATM in the player's own location when there is one; otherwise any, and the
                // arrow leads them across by the travel square.
                var here = PlayerLocation();
                AtmStation best = null;
                float bestD = float.MaxValue;
                for (int i = 0; i < _atms.Length; i++)
                {
                    var a = _atms[i];
                    if (!Live(a) || a.deposit) continue;
                    float d = DistanceToPlayer(a);
                    if (here != null && a.GetComponentInParent<Location>() != here) d += 1e6f;
                    if (d < bestD) { bestD = d; best = a; }
                }
                if (best != null) Consider(best, 0d, "Get cash at the ATM");
            }
        }

        private void Consider(StationBase station, double price, string label)
        {
            float dist = DistanceToPlayer(station);

            if (_best == null || price < _bestPrice || (price == _bestPrice && dist < _bestDist))
            {
                _second = _best; _secondPrice = _bestPrice; _secondDist = _bestDist; _secondLabel = _bestLabel;
                _best = station; _bestPrice = price; _bestDist = dist; _bestLabel = label;
            }
            else if (_second == null || price < _secondPrice || (price == _secondPrice && dist < _secondDist))
            {
                _second = station; _secondPrice = price; _secondDist = dist; _secondLabel = label;
            }
        }

        // ---- locations -------------------------------------------------------------------

        /// <summary>The location the player is in: the one whose root is nearest.</summary>
        private Location PlayerLocation()
        {
            if (_player == null || _locations == null) return null;

            Location best = null;
            float bestD = float.MaxValue;
            foreach (var loc in _locations)
            {
                if (loc == null) continue;
                float d = (loc.transform.position - _player.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = loc; }
            }
            return best;
        }

        /// <summary>
        /// The square the arrow should lead the player to for this line. When the line is in
        /// the other location that is the travel square that goes there, not the line's own.
        /// </summary>
        private StationBase ResolveForArrow(StationBase target)
        {
            if (!Live(target)) return null;

            var here = PlayerLocation();
            var there = target.GetComponentInParent<Location>();
            if (here == null || there == null || here == there) return target;

            foreach (var travel in _travels)
            {
                if (!Live(travel) || travel.destination != there) continue;
                if (travel.GetComponentInParent<Location>() == here) return travel;
            }
            return null;
        }
    }
}
