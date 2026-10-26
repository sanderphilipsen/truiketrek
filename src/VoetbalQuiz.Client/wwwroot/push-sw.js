// Pushmeldingen in de service worker (geladen door service-worker.js en service-worker.published.js).
// De server stuurt JSON: { title, body, url, tag } (zie PushPayload). Een tik opent de app op die pagina.

self.addEventListener('push', event => {
    let data = {};
    try { data = event.data ? event.data.json() : {}; } catch { }
    event.waitUntil(self.registration.showNotification(data.title || 'Truitje Trek', {
        body: data.body || '',
        icon: 'icon-192.png',
        badge: 'icon-192.png',
        lang: 'nl',
        tag: data.tag || undefined,
        data: { url: data.url || '' },
    }));
});

self.addEventListener('notificationclick', event => {
    event.notification.close();
    const target = new URL(event.notification.data?.url || '', self.registration.scope).href;
    event.waitUntil((async () => {
        // Staat de app al open, dan daar verder; anders een nieuw venster.
        const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
        const open = windows.find(w => w.url.startsWith(self.registration.scope));
        if (open) {
            await open.focus();
            if (open.url !== target) await open.navigate(target).catch(() => { });
            return;
        }
        await self.clients.openWindow(target);
    })());
});
