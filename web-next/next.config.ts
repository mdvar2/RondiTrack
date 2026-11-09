import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  reactCompiler: true,
  // Cache Components makes the page cacheable by route and lets us opt back
  // into partial data loading without prompting the whole route to block.
  cacheComponents: true,
  partialPrefetching: true,
  // No AGENTS.md written by `next dev`.
  agentRules: false,
  // BFF routes and proof downloads are private: never indexed, archived or
  // followed, even if a URL leaks. A header, because they aren't HTML pages.
  async headers() {
    const noRobots = [{ key: 'X-Robots-Tag', value: 'noindex, nofollow, noarchive' }]
    return [
      { source: '/bff/:path*', headers: noRobots },
      { source: '/api/:path*', headers: noRobots },
    ]
  },
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
};

export default nextConfig;
