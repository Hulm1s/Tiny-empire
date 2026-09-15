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
                });
            }

            Debug.Log($"[Audit] {entries.Count} squares");

            int clashes = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                for (int j = i + 1; j < entries.Count; j++)
                {
                    var a = entries[i];
                    var b = entries[j];
                    if (!a.Card.Overlaps(b.Card)) continue;

                    // A gate sits on top of whatever it unlocks, by design - but only one of
                    // the pair is ever active, so the player can never be in both.
                    if (!a.Active || !b.Active) continue;

                    clashes++;
                    var shared = Intersection(a.Card, b.Card);
                    Debug.LogWarning(
                        $"[Audit] OVERLAP {a.Name} x {b.Name} " +
                        $"by {shared.width:0.00} x {shared.height:0.00} m " +
                        $"(active: {a.Active}/{b.Active})");
                }
            }

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
