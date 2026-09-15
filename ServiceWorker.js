// Offline cache for the game.
//
// Strategy is split on purpose:
//   * index.html / manifest  -> network first, so republishing the game actually reaches
//                               players instead of being trapped behind a stale cache.
//   * Build + TemplateData   -> stale-while-revalidate, so launches are instant and the new
//                               version is quietly fetched in the background for next time.
//
// Getting this wrong is the classic "I uploaded a new build and she still sees the old one"
// bug, which is very hard to talk someone through over the phone.

const CACHE_NAME = "Home Made-Tiny Empire-2026.09.15.2040";

const PRECACHE = [
  "index.html",
  "manifest.webmanifest",
  "TemplateData/style.css",
  "TemplateData/icons/icon-180.png",
  "TemplateData/icons/icon-192.png",
  "TemplateData/icons/icon-512.png",
  "Build/docs.loader.js",
  "Build/docs.framework.js.unityweb",
  "Build/docs.data.unityweb",
  "Build/docs.wasm.unityweb",
];

self.addEventListener("install", function (event) {
  // Take over as soon as the new worker is ready rather than waiting for every tab to close.
  self.skipWaiting();
  event.waitUntil(
    caches.open(CACHE_NAME).then(function (cache) {
      // addAll fails the whole install if any single file 404s, which would leave the game
      // with no offline cache at all. Add them individually and tolerate misses.
      return Promise.all(PRECACHE.map(function (url) {
        return cache.add(url).catch(function () { /* optional asset, ignore */ });
      }));
    })
  );
});

self.addEventListener("activate", function (event) {
  event.waitUntil(
    caches.keys().then(function (names) {
      return Promise.all(names.map(function (name) {
        if (name !== CACHE_NAME) return caches.delete(name);
        return null;
      }));
    }).then(function () {
      return self.clients.claim();
    })
  );
});

self.addEventListener("fetch", function (event) {
  const request = event.request;
  if (request.method !== "GET") return;

  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return;

  const isShell = url.pathname.endsWith("/") ||
    url.pathname.endsWith("index.html") ||
    url.pathname.endsWith("manifest.webmanifest");

  if (isShell) {
    event.respondWith(networkFirst(request));
  } else {
    event.respondWith(staleWhileRevalidate(request));
  }
});

function networkFirst(request) {
  return fetch(request).then(function (response) {
    if (response && response.ok) {
      const copy = response.clone();
      caches.open(CACHE_NAME).then(function (cache) { cache.put(request, copy); });
    }
    return response;
  }).catch(function () {
    return caches.match(request).then(function (cached) {
      return cached || caches.match("index.html");
    });
  });
}

function staleWhileRevalidate(request) {
  return caches.open(CACHE_NAME).then(function (cache) {
    return cache.match(request).then(function (cached) {
      const network = fetch(request).then(function (response) {
        if (response && response.ok) cache.put(request, response.clone());
        return response;
      }).catch(function () {
        return cached;
      });
      return cached || network;
    });
  });
}
