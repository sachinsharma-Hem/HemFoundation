(function () {
    'use strict';
    //test
    // Update 'version' if you need to refresh the cache
    const version = 'v1.0::CacheFirstSafe';
    const offlineUrl = 'offline.html'; // Replace with your offline page filename
    const urlsToCache = [
        '/', // Root path
        offlineUrl
        //,
        //// Add other URLs of static assets you want to cache
        //'index.css',
        //'main.js',
        //'images/logo.png'
    ];

    // Store core files in a cache (including a page to display when offline)
    function updateStaticCache() {
        return caches.open(version)
            .then(cache => cache.addAll(urlsToCache));
    }

    function addToCache(request, response) {
        if (!response.ok && response.type !== 'opaque') {
            return;
        }

        const copy = response.clone();
        caches.open(version)
            .then(cache => cache.put(request, copy));
    }

    function serveOfflineImage(request) {
        if (request.headers.get('Accept').indexOf('image') !== -1) {
            return new Response('<svg role="img" aria-labelledby="offline-title" viewBox="0 0 400 300" xmlns="http://www.w3.org/2000/svg"><title id="offline-title">Offline</title><g fill="none" fill-rule="evenodd"><path fill="#D8D8D8" d="M0 0h400v300H0z"/><text fill="#9B9B9B" font-family="Helvetica Neue,Arial,Helvetica,sans-serif" font-size="72" font-weight="bold"><tspan x="93" y="172">offline</tspan></text></g></svg>', { headers: { 'Content-Type': 'image/svg+xml' } });
        }
    }

    self.addEventListener('install', function (event) {
        event.waitUntil(updateStaticCache());
    });

    self.addEventListener('activate', function (event) {
        event.waitUntil(
            caches.keys()
                .then(keys =>
                    Promise.all(
                        keys
                            .filter(key => key.indexOf(version) !== 0)
                            .map(key => caches.delete(key))
                    )
                )
        );
    });

    self.addEventListener('fetch', function (event) {
        const request = event.request;

        // Always fetch non-GET requests from the network
        if (request.method !== 'GET' || request.url.match(/\/browserLink/ig)) {
            event.respondWith(
                fetch(request)
                    .catch(() => caches.match(offlineUrl))
            );
            return;
        }

        // For HTML requests, try the network first, fall back to the cache, finally the offline page
        if (request.headers.get('Accept').indexOf('text/html') !== -1) {
            event.respondWith(
                fetch(request)
                    .then(response => {
                        addToCache(request, response);
                        return response;
                    })
                    .catch(() => caches.match(request)
                        .then(response => response || caches.match(offlineUrl))
                    )
            );
            return;
        }

        // Cache first for fingerprinted resources
        if (request.url.match(/(\?|&)v=/ig)) {
            event.respondWith(
                caches.match(request)
                    .then(response => response || fetch(request)
                        .then(response => {
                            addToCache(request, response);
                            return response || serveOfflineImage(request);
                        })
                        .catch(() => serveOfflineImage(request))
                    )
            );
            return;
        }

        // Network first for non-fingerprinted resources
        event.respondWith(
            fetch(request)
                .then(response => {
                    addToCache(request, response);
                    return response;
                })
                .catch(() => caches.match(request)
                    .then(response => response || serveOfflineImage(request))
                )
        );
    });
})();