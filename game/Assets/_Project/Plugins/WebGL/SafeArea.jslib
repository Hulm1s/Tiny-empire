// Reads the browser's safe-area insets (the notch, the status bar, the home indicator) for HudRoot.
//
// Unity's WebGL player cannot see them: Screen.safeArea is always the whole screen there. The
// page (WebGLTemplates/MobilePWA/index.html) draws edge to edge, and CSS env(safe-area-inset-*)
// is the only place the real values live. A hidden probe div whose padding is those env()
// values lets script read them back as numbers.
//
// Writes four floats into out[0..3] = top, right, bottom, left, in CANVAS pixels (the same units
// as Screen.width / Screen.height), and returns 1. Returns 0 if the page has no probe, in which
// case the caller falls back to Screen.safeArea.
//
// CSS px -> canvas px is canvas.width / canvas.clientWidth. That ratio is not the device pixel
// ratio: the template caps devicePixelRatio at 2 and Unity may render at a reduced scale, so
// measuring the canvas itself is the only thing that is always right.
mergeInto(LibraryManager.library, {
  Tycoon_GetSafeInsets: function (out) {
    var probe = document.getElementById("safe-area-probe");
    var canvas = Module.canvas || document.getElementById("unity-canvas");
    if (!probe || !canvas || !canvas.clientWidth || !canvas.clientHeight) return 0;

    var style = window.getComputedStyle(probe);
    var scaleX = canvas.width / canvas.clientWidth;
    var scaleY = canvas.height / canvas.clientHeight;
    var base = out >> 2;

    HEAPF32[base]     = (parseFloat(style.paddingTop)    || 0) * scaleY;
    HEAPF32[base + 1] = (parseFloat(style.paddingRight)  || 0) * scaleX;
    HEAPF32[base + 2] = (parseFloat(style.paddingBottom) || 0) * scaleY;
    HEAPF32[base + 3] = (parseFloat(style.paddingLeft)   || 0) * scaleX;
    return 1;
  }
});
