using System.Collections.Generic;
using Tycoon.Stations;
using Tycoon.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tycoon.Tasks
{
    /// <summary>
    /// The one arrow that tells the player where to go.
    ///
    /// A single screen-space image on the HUD, never a world object, with two behaviours that
    /// blend into one another rather than switch:
    /// <list type="bullet">
    /// <item><b>Target off screen:</b> pinned to the edge of the safe area, turned toward the
    /// target, sliding along the edge as the player moves. It keeps out of the money readout,
    /// the pause button and the task card, and takes the nearest free stretch of edge instead.</item>
    /// <item><b>Target on screen:</b> glides to just above the square, turns to point down and
    /// bounces gently.</item>
    /// </list>
    /// The change between the two is eased over a quarter of a second, so it never jumps.
    ///
    /// What it points at is decided by <see cref="TaskBoard"/>, which also swaps in the travel
    /// square when the target is in the other location. This class only draws. It is hidden while
    /// the player stands in the target square (they have arrived) and when there is no target.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class GuideArrow : MonoBehaviour
    {
        // The approved colours from the arrow sheet.
        private static readonly Color Green = new Color(0.30f, 0.78f, 0.34f);
        private static readonly Color Red = new Color(0.90f, 0.26f, 0.22f);
        private static readonly Color Gold = new Color(1f, 0.78f, 0.16f);

        /// <summary>Canvas units. The sprite is 256 px; this is its drawn size.</summary>
        private const float Size = 180f;

        /// <summary>How far the arrow's centre is kept from the edge of the safe area.</summary>
        private const float EdgeInset = 95f;

        /// <summary>Centre of the sprite to its tip: the tip sits 24 px up from the bottom of 256.</summary>
        private const float CentreToTip = Size * 0.406f;

        /// <summary>Gap between the arrow's tip and the square, before the bounce.</summary>
        private const float HoverGap = 34f;

        private const float BounceHeight = 16f;
        private const float BlendSeconds = 0.25f;
        private const float FadeSeconds = 0.18f;

        private const string EnabledPref = "tycoon.guideArrow";
        private static int _enabledState = -1;

        /// <summary>
        /// Whether the arrow is shown at all (pause menu switch, default on). The task list
        /// is unaffected. Persisted in PlayerPrefs.
        /// </summary>
        public static bool Enabled
        {
            get
            {
                if (_enabledState < 0) _enabledState = PlayerPrefs.GetInt(EnabledPref, 1) == 1 ? 1 : 0;
                return _enabledState == 1;
            }
            set
            {
                _enabledState = value ? 1 : 0;
                PlayerPrefs.SetInt(EnabledPref, _enabledState);
                PlayerPrefs.Save();
            }
        }

        private RectTransform _area;
        private RectTransform _rect;
        private Image _image;
        private readonly List<RectTransform> _avoid = new List<RectTransform>(4);
        private readonly Vector3[] _corners = new Vector3[4];

        private bool _onScreen;
        private float _blend;           // 0 = pinned to the edge, 1 = over the square
        private float _alpha;
        private Vector2 _edgePos;
        private Vector2 _edgeVelocity;
        private Vector2 _pos;
        private Vector2 _posVelocity;
        private bool _placed;
        private float _bounce;
        private TaskKind _spriteKind = (TaskKind)(-1);
        private StationBase _lastTarget;

        public static GuideArrow Create(RectTransform area)
        {
            var rect = UIFactory.CreateRect("GuideArrow", area);
            var arrow = rect.gameObject.AddComponent<GuideArrow>();
            arrow._area = area;
            arrow._rect = rect;

            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Size, Size);

            arrow._image = rect.gameObject.AddComponent<Image>();
            arrow._image.raycastTarget = false;
            arrow._image.preserveAspect = true;
            arrow._image.enabled = false;
            return arrow;
        }

        /// <summary>A piece of HUD the arrow must not sit on while it is pinned to the edge.</summary>
        public void AddAvoid(RectTransform rect)
        {
            if (rect != null && !_avoid.Contains(rect)) _avoid.Add(rect);
        }

        private static Color ColourFor(TaskKind kind) =>
            kind == TaskKind.Tutorial ? Green : kind == TaskKind.Problem ? Red : Gold;

        private void LateUpdate()
        {
            var board = TaskBoard.Instance;
            var camera = WorldUi.Camera;
            var target = board != null ? board.ArrowTarget : null;

            Rect area = _area.rect;
            bool canShow = Enabled && target != null && camera != null && area.width > 1f && area.height > 1f
                           && target.isActiveAndEnabled && !target.HasPlayer;

            float dt = Time.unscaledDeltaTime;
            _alpha = Mathf.MoveTowards(_alpha, canShow ? 1f : 0f, dt / FadeSeconds);

            if (_alpha <= 0.001f)
            {
                if (_image.enabled) _image.enabled = false;
                _placed = false;
                _lastTarget = null;
                return;
            }

            // Keep drawing (fading out) on the last target if it has just gone.
            if (!canShow) target = _lastTarget;
            if (target == null || camera == null) return;
            _lastTarget = target;

            if (board != null && board.Followed != _spriteKind)
            {
                _spriteKind = board.Followed;
                _image.sprite = IconFactory.GuideArrow(ColourFor(_spriteKind));
            }

            if (!_image.enabled) _image.enabled = true;

            // ---- where the target is, in the safe area's own coordinates ----
            Vector3 world = target.transform.position + Vector3.up * 0.1f;
            Vector3 screen = camera.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, screen, null, out Vector2 targetLocal);

            var inset = Rect.MinMaxRect(area.xMin + EdgeInset, area.yMin + EdgeInset,
                                        area.xMax - EdgeInset, area.yMax - EdgeInset);
            Vector2 centre = area.center;

            // ---- edge mode: the nearest free stretch of edge toward the target ----
            Vector2 wanted = PerimeterPoint(inset, centre, targetLocal);
            Vector2 edgeGoal = NearestFreeEdge(inset, wanted);

            // ---- target mode: just above the square ----
            Vector2 spot = targetLocal + new Vector2(0f, CentreToTip + HoverGap);

            // Hysteresis, so a target hovering at the margin does not flicker between modes.
            float margin = _onScreen ? 0f : 24f;
            bool visibleSpot = spot.x > inset.xMin + margin && spot.x < inset.xMax - margin &&
                               spot.y > inset.yMin + margin && spot.y < inset.yMax - margin &&
                               !InsideAvoid(spot, margin);
            _onScreen = visibleSpot;

            if (!_placed)
            {
                // First frame after being hidden: appear where it belongs rather than flying in.
                _blend = _onScreen ? 1f : 0f;
                _edgePos = edgeGoal;
                _pos = _onScreen ? spot : edgeGoal;
                _posVelocity = Vector2.zero;
                _edgeVelocity = Vector2.zero;
                _placed = true;
            }

            _blend = Mathf.MoveTowards(_blend, _onScreen ? 1f : 0f, dt / BlendSeconds);
            float ease = _blend * _blend * (3f - 2f * _blend);

            _edgePos = Vector2.SmoothDamp(_edgePos, edgeGoal, ref _edgeVelocity, 0.08f, Mathf.Infinity, dt);

            _bounce += dt * 5.2f;
            float hop = Mathf.Abs(Mathf.Sin(_bounce)) * BounceHeight;

            Vector2 goal = Vector2.Lerp(_edgePos, spot + new Vector2(0f, hop), ease);
            _pos = Vector2.SmoothDamp(_pos, goal, ref _posVelocity, 0.06f, Mathf.Infinity, dt);

            // The sprite points down; this turns it toward the target from where it now sits.
            Vector2 toTarget = targetLocal - _pos;
            float edgeAngle = toTarget.sqrMagnitude < 1f
                ? 0f
                : Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg + 90f;
            float angle = Mathf.LerpAngle(edgeAngle, 0f, ease);

            _rect.anchoredPosition = _pos - centre;
            _rect.localRotation = Quaternion.Euler(0f, 0f, angle);

            var colour = _image.color;
            if (!Mathf.Approximately(colour.a, _alpha))
            {
                colour.a = _alpha;
                _image.color = colour;
            }
        }

        // ---- geometry ---------------------------------------------------------------------

        /// <summary>Where the ray from the middle of the area to the target leaves the inset rectangle.</summary>
        private static Vector2 PerimeterPoint(Rect inset, Vector2 centre, Vector2 target)
        {
            Vector2 d = target - centre;
            if (d.sqrMagnitude < 0.01f) d = Vector2.down;

            float halfW = inset.width * 0.5f;
            float halfH = inset.height * 0.5f;
            float tx = Mathf.Abs(d.x) < 0.0001f ? float.MaxValue : halfW / Mathf.Abs(d.x);
            float ty = Mathf.Abs(d.y) < 0.0001f ? float.MaxValue : halfH / Mathf.Abs(d.y);
            return centre + d * Mathf.Min(tx, ty);
        }

        private Rect LocalRect(RectTransform rt, float inflate)
        {
            rt.GetWorldCorners(_corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                Vector3 local = _area.InverseTransformPoint(_corners[i]);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            return Rect.MinMaxRect(min.x - inflate, min.y - inflate, max.x + inflate, max.y + inflate);
        }

        private bool InsideAvoid(Vector2 p, float extra)
        {
            for (int i = 0; i < _avoid.Count; i++)
            {
                if (_avoid[i] == null || !_avoid[i].gameObject.activeInHierarchy) continue;
                if (LocalRect(_avoid[i], Size * 0.5f + extra).Contains(p)) return true;
            }
            return false;
        }

        // Scratch space for the stretches of edge cut out by the HUD.
        private readonly float[] _cutLo = new float[8];
        private readonly float[] _cutHi = new float[8];

        /// <summary>
        /// The point on the inset rectangle's outline, not covered by any HUD element, that is
        /// closest to <paramref name="wanted"/>. Worked out edge by edge so the arrow slides
        /// smoothly along whatever stretch is free instead of hopping between sample points.
        /// </summary>
        private Vector2 NearestFreeEdge(Rect inset, Vector2 wanted)
        {
            float bestD = float.MaxValue;
            Vector2 best = wanted;

            ConsiderEdge(true, inset.yMax, inset.xMin, inset.xMax, wanted, ref best, ref bestD);
            ConsiderEdge(true, inset.yMin, inset.xMin, inset.xMax, wanted, ref best, ref bestD);
            ConsiderEdge(false, inset.xMin, inset.yMin, inset.yMax, wanted, ref best, ref bestD);
            ConsiderEdge(false, inset.xMax, inset.yMin, inset.yMax, wanted, ref best, ref bestD);
            return best;
        }

        private void ConsiderEdge(bool horizontal, float fixedCoord, float lo, float hi,
            Vector2 wanted, ref Vector2 best, ref float bestD)
        {
            int cuts = 0;
            for (int i = 0; i < _avoid.Count && cuts < _cutLo.Length; i++)
            {
                if (_avoid[i] == null || !_avoid[i].gameObject.activeInHierarchy) continue;

                Rect r = LocalRect(_avoid[i], Size * 0.5f);
                float cross = horizontal ? r.yMin : r.xMin;
                float crossEnd = horizontal ? r.yMax : r.xMax;
                if (fixedCoord <= cross || fixedCoord >= crossEnd) continue;

                float from = horizontal ? r.xMin : r.yMin;
                float to = horizontal ? r.xMax : r.yMax;

                // Insertion sort by start, so the free pieces can be read off in order.
                int at = cuts++;
                while (at > 0 && _cutLo[at - 1] > from)
                {
                    _cutLo[at] = _cutLo[at - 1];
                    _cutHi[at] = _cutHi[at - 1];
                    at--;
                }
                _cutLo[at] = from;
                _cutHi[at] = to;
            }

            float cursor = lo;
            for (int i = 0; i < cuts; i++)
            {
                if (_cutLo[i] > cursor) ConsiderPiece(horizontal, fixedCoord, cursor, Mathf.Min(_cutLo[i], hi), wanted, ref best, ref bestD);
                cursor = Mathf.Max(cursor, _cutHi[i]);
            }
            if (cursor < hi) ConsiderPiece(horizontal, fixedCoord, cursor, hi, wanted, ref best, ref bestD);
        }

        private static void ConsiderPiece(bool horizontal, float fixedCoord, float a, float b,
            Vector2 wanted, ref Vector2 best, ref float bestD)
        {
            if (b - a < 1f) return;

            Vector2 p = horizontal
                ? new Vector2(Mathf.Clamp(wanted.x, a, b), fixedCoord)
                : new Vector2(fixedCoord, Mathf.Clamp(wanted.y, a, b));

            float d = (p - wanted).sqrMagnitude;
            if (d < bestD) { bestD = d; best = p; }
        }
    }
}
