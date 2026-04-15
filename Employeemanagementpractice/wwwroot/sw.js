// BA Academy Service Worker - PWA + Offline Support
const CACHE_NAME = 'ba-academy-v2';
const OFFLINE_URL = '/offline.html';

// Assets to cache for offline/fast loading
const PRECACHE_ASSETS = [
    '/',
    '/css/site.css',
    '/js/site.js',
    '/lib/bootstrap/dist/css/bootstrap.min.css',
    '/lib/bootstrap/dist/js/bootstrap.bundle.min.js',
    '/lib/jquery/dist/jquery.min.js',
    '/images/ba-academy-logo.svg',
    '/manifest.json',
    '/offline.html'
];

// Install - precache essential assets
self.addEventListener('install', function(event) {
    event.waitUntil(
        caches.open(CACHE_NAME).then(function(cache) {
            return cache.addAll(PRECACHE_ASSETS).catch(function(err) {
                console.log('Precache failed for some assets:', err);
            });
        })
    );
    self.skipWaiting();
});

// Activate - clean up old caches
self.addEventListener('activate', function(event) {
    event.waitUntil(
        caches.keys().then(function(cacheNames) {
            return Promise.all(
                cacheNames.filter(function(name) {
                    return name !== CACHE_NAME;
                }).map(function(name) {
                    return caches.delete(name);
                })
            );
        })
    );
    self.clients.claim();
});

// Fetch - network first, fallback to cache
self.addEventListener('fetch', function(event) {
    if (event.request.method !== 'GET') return;
    var url = event.request.url;
    if (url.includes('/notificationHub') || url.includes('/api/')) return;

    event.respondWith(
        fetch(event.request).then(function(response) {
            if (response.status === 200) {
                var shouldCache = url.includes('/css/') || url.includes('/js/') || url.includes('/lib/') || url.includes('/images/');
                // Also cache navigation pages for authenticated users
                if (event.request.mode === 'navigate') shouldCache = true;
                if (shouldCache) {
                    var responseClone = response.clone();
                    caches.open(CACHE_NAME).then(function(cache) {
                        cache.put(event.request, responseClone);
                    });
                }
            }
            return response;
        }).catch(function() {
            return caches.match(event.request).then(function(cachedResponse) {
                if (cachedResponse) return cachedResponse;
                if (event.request.mode === 'navigate') {
                    return caches.match(OFFLINE_URL);
                }
            });
        })
    );
});

// Listen for sync events (offline clock data)
self.addEventListener('sync', function(event) {
    if (event.tag === 'sync-clock-data') {
        event.waitUntil(syncPendingClocks());
    }
});

async function syncPendingClocks() {
    // Communicate with IndexedDB via the client
    var clients = await self.clients.matchAll();
    clients.forEach(function(client) {
        client.postMessage({ type: 'SYNC_PENDING_CLOCKS' });
    });
}

// Listen for messages from the main app
self.addEventListener('message', function(event) {
    if (event.data && event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
});
