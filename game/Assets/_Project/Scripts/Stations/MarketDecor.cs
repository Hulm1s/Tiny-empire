using System;
using Tycoon.Core;
using Tycoon.UI;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// What the shop looks like: wall pattern, three colours, and the floor.
    /// Serialisable by itself so it saves as one small struct.
    /// </summary>
    [Serializable]
    public struct DecorLook
    {
        public int pattern;
        public Color primary, secondary, tertiary;
        public int floorKind;
        public Color floorColor;

        [Tooltip("0 = light oak, 1 = walnut. Only used by the wooden floor.")]
        public float woodShade;

        /// <summary>
        /// Cream walls with a red skirting over pale tiles: the look the shop had when it had a
        /// single paint gate, and the first thing the paint menu offers.
        /// </summary>
        public static DecorLook Default => new DecorLook
        {
            pattern = PatternFactory.Plain,
            primary = new Color(0.96f, 0.92f, 0.82f),
            secondary = new Color(0.62f, 0.78f, 0.86f),
            tertiary = new Color(0.82f, 0.28f, 0.28f),
            floorKind = PatternFactory.FloorTiles,
            floorColor = new Color(0.84f, 0.82f, 0.77f),
            woodShade = 0.3f,
        };

        public bool SameAs(DecorLook o) =>
            pattern == o.pattern && primary == o.primary && secondary == o.secondary &&
            tertiary == o.tertiary && floorKind == o.floorKind && floorColor == o.floorColor &&
            Mathf.Approximately(woodShade, o.woodShade);
    }

    /// <summary>
    /// A piece of the shop the paint job changes: which renderer, what it is, and how big, so
    /// the pattern can be tiled to a constant scale.
    /// </summary>
    [Serializable]
    public class DecorSurface
    {
        public enum Kind { Wall, Floor, Trim }

        public Renderer renderer;
        public Kind kind;

        [Tooltip("Metres along the face the pattern runs over (a wall's length, the floor's width).")]
        public float length = 1f;

        [Tooltip("Metres up a wall's face, or the floor's depth.")]
        public float height = 1f;

        [Tooltip("Where the face starts in the market's own grid, so the pattern carries on " +
                 "unbroken from one wall piece to the next.")]
        public Vector2 origin;
    }

    /// <summary>
    /// The shop's interior look, and the thing that applies it.
    ///
    /// The shop has two complete coats built into the scene, a grubby one and a painted one, and
    /// the state of this component decides which is showing. The painted coat is the one that
    /// changes: its walls, skirting and floor are drawn with the SHARED material assets they
    /// were built with, and each is given its pattern through a MaterialPropertyBlock - a
    /// texture, a tint, and a tiling worked out from the piece's real size. Nothing here ever
    /// creates a Material, which renders magenta in the URP web build.
    ///
    /// Until the first paint job is confirmed the shop is the derelict. The paint menu previews
    /// by calling <see cref="Preview"/>, which shows the painted coat with the unconfirmed look
    /// and puts it back with <see cref="CancelPreview"/>; only <see cref="Commit"/> keeps it.
    ///
    /// Saved as one small blob under its own id (<c>market.decor</c>).
    /// </summary>
    public class MarketDecor : MonoBehaviour, ISaveable
    {
        [Header("The two coats")]
        [Tooltip("Shown until the first paint job is confirmed.")]
        public GameObject[] grubby;

        [Tooltip("Shown from the first confirmed paint job (and while previewing one).")]
        public GameObject[] painted;

        [Header("The sign over the shelves")]
        public GameObject signDirty;
        public GameObject signClean;

        [Tooltip("Once OPEN is done the lit sign replaces the clean one.")]
        public ChoreStation openStep;

        [Header("What gets patterned")]
        public DecorSurface[] surfaces;

        private DecorLook _look = DecorLook.Default;
        private bool _painted;
        private bool _previewing;
        private DecorLook _shown;

        private Texture2D _wallTexture;
        private Texture2D _floorTexture;

        private MaterialPropertyBlock _block;

        /// <summary>True once the first paint job has been paid for and confirmed.</summary>
        public bool HasPainted => _painted;

        /// <summary>The look that has been confirmed, or the default if none has.</summary>
        public DecorLook Look => _look;

        public string SaveKey => SaveKeys.For(this);

        private void OnEnable() => SaveSystem.Register(this);
        private void OnDisable() => SaveSystem.Unregister(this);

        private void Start() => Refresh();

        // ---------------------------------------------------------------- the paint menu

        /// <summary>Shows an unconfirmed look on the walls and floor, for the menu to play with.</summary>
        public void Preview(DecorLook look)
        {
            _previewing = true;
            _shown = look;
            Refresh();
        }

        /// <summary>Puts back whatever was confirmed - the derelict, if nothing was.</summary>
        public void CancelPreview()
        {
            _previewing = false;
            Refresh();
        }

        /// <summary>Keeps a look. The menu has taken the money by now.</summary>
        public void Commit(DecorLook look)
        {
            _look = look;
            _painted = true;
            _previewing = false;
            Refresh();
        }

        // ---------------------------------------------------------------- applying

        private void Refresh()
        {
            bool showPainted = _painted || _previewing;
            DecorLook look = _previewing ? _shown : _look;

            SetAll(grubby, !showPainted);
            SetAll(painted, showPainted);

            if (signDirty != null) signDirty.SetActive(!showPainted);

            // The clean sign is only for the stretch between painting and opening; OPEN hides
            // it itself and must win whichever of the two restores from a save first.
            bool opened = openStep != null && openStep.IsDone;
            if (signClean != null) signClean.SetActive(showPainted && !opened);

            if (showPainted) ApplyLook(look);
        }

        private static void SetAll(GameObject[] set, bool on)
        {
            if (set == null) return;
            foreach (var go in set)
                if (go != null && go.activeSelf != on) go.SetActive(on);
        }

        private void ApplyLook(DecorLook look)
        {
            if (surfaces == null) return;
            if (_block == null) _block = new MaterialPropertyBlock();

            if (_wallTexture == null) _wallTexture = PatternFactory.NewTexture("DecorWall");
            PatternFactory.PaintWall(look.pattern, look.primary, look.secondary, look.tertiary,
                _wallTexture);

            bool floorTextured = look.floorKind != PatternFactory.FloorSolid;
            Color floorTint = look.floorKind == PatternFactory.FloorWood
                ? PatternFactory.WoodColor(look.woodShade)
                : look.floorColor;

            if (floorTextured)
            {
                if (_floorTexture == null) _floorTexture = PatternFactory.NewTexture("DecorFloor");
                PatternFactory.PaintFloor(look.floorKind, floorTint, _floorTexture);
            }

            bool banded = PatternFactory.IsBanded(look.pattern);

            foreach (var s in surfaces)
            {
                if (s == null || s.renderer == null) continue;

                _block.Clear();
                switch (s.kind)
                {
                    case DecorSurface.Kind.Wall:
                    {
                        // A banded pattern is one whole wall tall, so it is stretched to the
                        // piece's own height instead of repeating up it.
                        float v = banded
                            ? s.height / PatternFactory.BandedHeight
                            : s.height / PatternFactory.TileMeters;
                        float u = s.length / PatternFactory.TileMeters;
                        SetMap(_block, _wallTexture, new Vector4(u, v,
                            s.origin.x / PatternFactory.TileMeters,
                            banded ? 0f : s.origin.y / PatternFactory.TileMeters));
                        Tint(_block, Color.white);
                        break;
                    }

                    case DecorSurface.Kind.Floor:
                        if (floorTextured)
                        {
                            SetMap(_block, _floorTexture, new Vector4(
                                s.length / PatternFactory.TileMeters,
                                s.height / PatternFactory.TileMeters,
                                s.origin.x / PatternFactory.TileMeters,
                                s.origin.y / PatternFactory.TileMeters));
                            Tint(_block, Color.white);
                        }
                        else
                        {
                            Tint(_block, floorTint);
                        }
                        break;

                    case DecorSurface.Kind.Trim:
                        Tint(_block, look.tertiary);
                        break;
                }

                s.renderer.SetPropertyBlock(_block);
            }
        }

        // Both the URP and the built-in names are set. The build is URP, but a property a
        // shader does not have is simply ignored, and it keeps the editor fallback honest.
        private static void SetMap(MaterialPropertyBlock block, Texture2D texture, Vector4 tilingOffset)
        {
            block.SetTexture("_BaseMap", texture);
            block.SetVector("_BaseMap_ST", tilingOffset);
            block.SetTexture("_MainTex", texture);
            block.SetVector("_MainTex_ST", tilingOffset);
        }

        private static void Tint(MaterialPropertyBlock block, Color colour)
        {
            block.SetColor("_BaseColor", colour);
            block.SetColor("_Color", colour);
        }

        private void OnDestroy()
        {
            if (_wallTexture != null) Destroy(_wallTexture);
            if (_floorTexture != null) Destroy(_floorTexture);
        }

        // ---------------------------------------------------------------- saving

        [Serializable]
        private struct State
        {
            public bool painted;
            public DecorLook look;
        }

        public string CaptureState() => JsonUtility.ToJson(new State { painted = _painted, look = _look });

        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<State>(json);
            _painted = s.painted;
            if (s.painted)
            {
                _look = s.look;
                // A save from before a pattern existed can only hold indices that still do.
                _look.pattern = Mathf.Clamp(_look.pattern, 0, PatternFactory.PatternCount - 1);
                _look.floorKind = Mathf.Clamp(_look.floorKind, 0, 2);
            }
            _previewing = false;
            Refresh();
        }
    }
}
