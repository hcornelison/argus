export const environment = {
  // Same-origin: IIS serves the SPA and reverse-proxies these paths to the styx service.
  // If you instead point pantheon directly at styx, set absolute URLs here and enable CORS on styx.
  apiBase: '/api',
  hubUrl: '/hubs/live'
};
