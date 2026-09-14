using UnityEngine;

namespace Tycoon.UI
{
    /// <summary>
    /// Shared plumbing for the world-space UI: one cached camera, and an off-screen test.
    ///
    /// Every square, sign and bubble in the game is its own world-space Canvas, and a Canvas
    /// that changes anything at all rebuilds its mesh on the main thread. A full farm has
    /// around seventy of them, but the orthographic camera only ever shows a handful, so the
    /// cheapest possible optimisation is simply not to run the ones nobody can see.
    /// </summary>
    public static class WorldUi
    {
        private static Camera _camera;

        /// <summary>
        /// The camera, looked up once rather than per component per frame.
        ///
        /// <see cref="Camera.main"/> is a tag search. Called by three components across seventy
        /// objects it stops being free, and there is exactly one camera in this game.
        /// </summary>
        public static Camera Camera
        {
            get
            {
                if (_camera == null) _camera = Camera.main;
                return _camera;
            }
        }

        /// <summary>
        /// True when a point is on screen, or close enough to it to be worth drawing.
        ///
        /// <paramref name="margin"/> is a fraction of the screen, and needs to cover half the
        /// widest thing being tested: a five-metre field measured from its centre reaches about
        /// a quarter of the way across the view, and popping into existence at the screen edge
        /// would be far more noticeable than the work it saves.
        /// </summary>
        public static bool IsVisible(Vector3 worldPosition, float margin = 0.3f)
        {
            var camera = Camera;
            // No camera yet (the very first frames of a load) - draw everything rather than
            // flashing the whole world off.
            if (camera == null) return true;

            Vector3 view = camera.WorldToViewportPoint(worldPosition);
            if (view.z < 0f) return false;

            return view.x > -margin && view.x < 1f + margin
                && view.y > -margin && view.y < 1f + margin;
        }
    }
}
