using Tycoon.Stations;
using UnityEngine;
using UnityEngine.UI;

namespace Tycoon.UI
{
    /// <summary>
    /// The florist's stock, as a small info panel at the bottom of the screen: the icon of every flower that has
    /// been bought with how many are waiting behind the counter, "7 / 12", red at 0 - so the player can see why a
    /// waiting customer cannot be served. Shown only while the player stands inside a <see cref="StockInfoZone"/>
    /// (the florist's shop and meadow) and hidden everywhere else: the farm, the supermarket, the street.
    ///
    /// Information only: nothing in it is a button, and none of it takes a tap, so it never gets in the way of
    /// the joystick (which is a floating stick under the finger, anywhere in the lower part of the screen). It
    /// lives in the safe area, so the home bar and the notch never cover it, and it is sized in canvas units like
    /// the rest of the HUD, so it follows the portrait / wide layouts. It sits below the task card (top left), the
    /// money plaque (top) and the pause button (top right), and below every menu that opens over the HUD.
    /// </summary>
    public class StockPanel : MonoBehaviour
    {
        private const float CellWidth = 140f, PanelHeight = 112f, Pad = 14f, BottomMargin = 22f;
        private const int MaxCells = 4;

        private static readonly Color Ink = new Color(0.12f, 0.14f, 0.17f);
        private static readonly Color Empty = new Color(0.92f, 0.34f, 0.30f);

        private RectTransform _root;
        private RectTransform[] _cells;
        private Image[] _icons;
        private Text[] _texts;
        private int[] _counts;
        private int _mask = -1;
        private bool _shown;
        private float _timer, _findTimer;
        private StockInfoZone _zone;
        private Transform _player;

        public static StockPanel Create(RectTransform safeArea)
        {
            var go = new GameObject("StockPanel", typeof(RectTransform));
            go.transform.SetParent(safeArea, false);
            var holder = (RectTransform)go.transform;
            Stretch(holder, 0f);
            var panel = go.AddComponent<StockPanel>();
            // The visible panel is a child: the holder stays active so this keeps running while it is hidden.
            panel.Build(UIFactory.CreateRect("Panel", holder));
            return panel;
        }

        private void Build(RectTransform root)
        {
            _root = root;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(0f, BottomMargin);
            root.sizeDelta = new Vector2(Pad * 2f + CellWidth, PanelHeight);

            var border = UIFactory.CreatePanel("Border", root, new Color(0.13f, 0.15f, 0.19f, 0.88f));
            Stretch(border.rectTransform, 0f);
            var panel = UIFactory.CreatePanel("Panel", root, new Color(0.97f, 0.97f, 0.96f, 0.92f));
            Stretch(panel.rectTransform, 4f);

            _cells = new RectTransform[MaxCells];
            _icons = new Image[MaxCells];
            _texts = new Text[MaxCells];
            _counts = new int[MaxCells];
            for (int i = 0; i < MaxCells; i++)
            {
                var cell = UIFactory.CreateRect("Cell" + i, root);
                cell.anchorMin = cell.anchorMax = new Vector2(0f, 0.5f);
                cell.pivot = new Vector2(0.5f, 0.5f);
                cell.sizeDelta = new Vector2(CellWidth, PanelHeight - 12f);
                _cells[i] = cell;

                var icon = UIFactory.CreateRect("Icon", cell);
                _icons[i] = icon.gameObject.AddComponent<Image>();
                _icons[i].raycastTarget = false;
                _icons[i].preserveAspect = true;
                icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.68f);
                icon.pivot = new Vector2(0.5f, 0.5f);
                icon.sizeDelta = new Vector2(62f, 62f);

                _texts[i] = UIFactory.CreateText("Count", cell, "", 34);
                _texts[i].color = Ink;
                _texts[i].fontStyle = FontStyle.Bold;
                _texts[i].raycastTarget = false;
                var t = _texts[i].rectTransform;
                t.anchorMin = t.anchorMax = new Vector2(0.5f, 0.2f);
                t.pivot = new Vector2(0.5f, 0.5f);
                t.sizeDelta = new Vector2(CellWidth - 6f, 40f);
            }

            root.gameObject.SetActive(false);
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = 0.2f;

            // Both are looked up rarely: the zone belongs to the scene, the player may be created after it.
            _findTimer -= 0.2f;
            if (_zone == null && _findTimer <= 0f)
            {
                _zone = FindFirstObjectByType<StockInfoZone>(FindObjectsInactive.Include);
                _findTimer = 1f;
            }
            if (_player == null)
            {
                var go = GameObject.FindWithTag("Player");
                if (go != null) _player = go.transform;
            }

            bool inside = _zone != null && _player != null && _zone.Contains(_player.position);
            Apply(inside ? _zone.stocks : null);
        }

        /// <summary>For the preview pictures: shows these stocks whatever the player is doing.</summary>
        public void Preview(ItemBuffer[] stocks) => Apply(stocks);

        private void Apply(ItemBuffer[] stocks)
        {
            if (stocks == null || stocks.Length == 0)
            {
                if (_shown) { _shown = false; _root.gameObject.SetActive(false); }
                return;
            }

            int mask = 0, visible = 0;
            for (int i = 0; i < stocks.Length && i < MaxCells; i++)
                if (stocks[i] != null && stocks[i].gameObject.activeInHierarchy) { mask |= 1 << i; visible++; }

            if (visible == 0)
            {
                if (_shown) { _shown = false; _root.gameObject.SetActive(false); }
                return;
            }

            if (!_shown) { _shown = true; _root.gameObject.SetActive(true); _mask = -1; }

            if (mask != _mask)
            {
                _mask = mask;
                _root.sizeDelta = new Vector2(Pad * 2f + CellWidth * visible, PanelHeight);
                int slot = 0;
                for (int i = 0; i < MaxCells; i++)
                {
                    bool on = (mask & (1 << i)) != 0;
                    _cells[i].gameObject.SetActive(on);
                    if (!on) continue;
                    _cells[i].anchoredPosition = new Vector2(Pad + CellWidth * (slot + 0.5f), 0f);
                    slot++;
                    _counts[i] = -1;
                }
            }

            for (int i = 0; i < MaxCells; i++)
            {
                if ((mask & (1 << i)) == 0) continue;
                var buffer = stocks[i];
                int count = buffer.Count;
                if (count == _counts[i]) continue;
                _counts[i] = count;

                _texts[i].text = count + " / " + buffer.capacity;
                _texts[i].color = count <= 0 ? Empty : Ink;

                var sprite = IconFactory.For(buffer.item);
                _icons[i].sprite = sprite;
                _icons[i].enabled = sprite != null;
                _icons[i].color = buffer.item != null && !IconFactory.HasArtwork(buffer.item)
                    ? buffer.item.color
                    : (count <= 0 ? new Color(1f, 1f, 1f, 0.55f) : Color.white);
            }
        }
    }
}
