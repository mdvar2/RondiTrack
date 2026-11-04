import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  reactCompiler: true,
  // Cache Components makes the page cacheable by route and lets us opt back
  // into partial data loading without prompting the whole route to block.
  cacheComponents: true,
  partialPrefetching: true,
  // No AGENTS.md written by `next dev`.
  agentRules: false,
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
