using System.Collections.Generic;
using Tycoon.Stations;
using Tycoon.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Measures every interaction square in the built scene and reports overlaps.
    ///
    /// Two squares on top of each other means the player stands in both at once - buying a
    /// worker while harvesting, or paying into two purchases. Eyeballing the farm does not
    /// catch it reliably, and the numbers in the builder are easy to get subtly wrong, so
    /// the scene itself gets asked.
    /// </summary>
    public static class SquareAudit
    {
        private const string ScenePath = "Assets/_Project/Scenes/Farm.unity";

        /// <summary>Mirrors InteractionSquare: the drawn outline is never smaller than this.</summary>
        private static readonly Vector2 MinCard = new Vector2(2.5f, 2.1f);

        private class Entry
        {
            public string Name;
            public Rect Trigger;    // where the action actually works
            public Rect Card;       // what the player sees
            public bool Active;

            /// <summary>
            /// True once every purchase has been made: gates delete themselves, and everything
            /// they were hiding is on. That is the fullest the farm ever gets, and so the state
            /// most likely to have two squares fighting over the same patch of grass.
            /// </summary>
            public bool ActiveWhenBuilt;
        }

        /// <summary>
        /// Reports every pair of squares that share ground in one particular state of the farm.
        ///
        /// A gate sits exactly on top of whatever it unlocks, by design, so a pair is only a
        /// fault when both halves are live at the same moment - hence taking the state as a
        /// predicate rather than reading it off the scene.
        /// </summary>
        private static int Overlaps(List<Entry> entries, string state, System.Func<Entry, bool> live)
        {
            int clashes = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                for (int j = i + 1; j < entries.Count; j++)
                {
                    var a = entries[i];
                    var b = entries[j];
                    if (!live(a) || !live(b)) continue;
                    if (!a.Card.Overlaps(b.Card)) continue;

                    clashes++;
                    var shared = Intersection(a.Card, b.Card);
                    Debug.LogWarning(
                        $"[Audit] OVERLAP ({state}) {a.Name} x {b.Name} " +
                        $"by {shared.width:0.00} x {shared.height:0.00} m");
                }
            }

            Debug.Log($"[Audit] {state}: {clashes} overlapping pairs.");
            return clashes;
        }

        [MenuItem("Tycoon/Audit Interaction Squares")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var root = GameObject.Find("Farm");
            if (root == null)
            {
                Debug.LogError("[Audit] No Farm root in the scene.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            var entries = new List<Entry>();

            foreach (var station in Object.FindObjectsByType<StationBase>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var box = station.GetComponent<BoxCollider>();
                var square = station.GetComponent<InteractionSquare>();
                if (box == null) continue;

                Vector3 local = root.transform.InverseTransformPoint(station.transform.position);
                Vector2 triggerSize = new Vector2(box.size.x, box.size.z);
                Vector2 cardSize = square != null
                    ? new Vector2(Mathf.Max(square.size.x, MinCard.x),
                                  Mathf.Max(square.size.y, MinCard.y))
                    : triggerSize;

                entries.Add(new Entry
                {
                    Name = station.name + " (" + station.GetType().Name + ")",
                    Trigger = Centred(local, triggerSize),
                    Card = Centred(local, cardSize),
                    Active = station.gameObject.activeInHierarchy,
                    // Every UnlockStation hides itself once paid off (hideSelfOnUnlock), and
                    // everything else in the scene is either on already or revealed by one.
                    ActiveWhenBuilt = !(station is UnlockStation),
                });
            }

            Debug.Log($"[Audit] {entries.Count} squares");

            // Checked twice: as the game opens, and as it ends. An overlap that only appears
            // once the second coop has been bought is still an overlap the player will meet.
            int clashes = Overlaps(entries, "day one", e => e.Active)
                        + Overlaps(entries, "fully built", e => e.ActiveWhenBuilt);

            // A trigger noticeably bigger than its outline means the player can act from
            // somewhere the game never told them about, and vice versa.
            foreach (var e in entries)
            {
                float dx = Mathf.Abs(e.Trigger.width - e.Card.width);
                float dy = Mathf.Abs(e.Trigger.height - e.Card.height);
                if (dx > 0.8f || dy > 0.8f)
                    Debug.LogWarning($"[Audit] MISMATCH {e.Name}: trigger " +
                                     $"{e.Trigger.width:0.0}x{e.Trigger.height:0.0} vs outline " +
                                     $"{e.Card.width:0.0}x{e.Card.height:0.0}");
            }

            Debug.Log(clashes == 0 ? "[Audit] No overlaps." : $"[Audit] {clashes} overlapping pairs.");
            Debug.Log("AUDIT_OK");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static Rect Centred(Vector3 centre, Vector2 size) =>
            new Rect(centre.x - size.x * 0.5f, centre.z - size.y * 0.5f, size.x, size.y);

        private static Rect Intersection(Rect a, Rect b)
        {
            float x = Mathf.Max(a.xMin, b.xMin);
            float y = Mathf.Max(a.yMin, b.yMin);
            return new Rect(x, y, Mathf.Min(a.xMax, b.xMax) - x, Mathf.Min(a.yMax, b.yMax) - y);
        }
    }
}
