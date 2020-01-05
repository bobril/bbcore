// This is to force this to be compiled as module, without it next line does not work
export default null;
// Typescript does not have serviceworker.d.ts so this hack is needed to fix it, though never use global scope, use this `self` instead
declare var self: ServiceWorkerGlobalScope;

// These defines are automatically added by Bobril-build
declare var swBuildDate: string;
declare var swBuildId: string;
declare var swFiles: string[];

const swCacheName = "app_" + swBuildId;

self.addEventListener("install", function(e) {
    console.log("SW Install version built " + swBuildDate);
    // cache all files (it does no include sw.js itself), but we need to add index.html under "/" url
    e.waitUntil(
        caches.open(swCacheName).then(function(cache) {
            return cache.addAll(swFiles.concat(["/"]));
        })
    );
});

self.addEventListener("activate", function(e) {
    console.log("SW Activated version built " + swBuildDate);
    // remove all old versions from cache
    e.waitUntil(
        caches.keys().then(function(cacheNames) {
            return Promise.all(
                cacheNames
                    .filter(function(cacheName) {
                        return cacheName.startsWith("app_") && cacheName != swCacheName;
                    })
                    .map(function(cacheName) {
                        return caches.delete(cacheName);
                    })
            );
        })
    );
});

self.addEventListener("fetch", function(e) {
    console.log("SW fetch", e.request.url);
    e.respondWith(
        caches.match(e.request).then(function(response) {
            return response || fetch(e.request);
        })
    );
});
