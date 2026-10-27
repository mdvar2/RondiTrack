import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  reactCompiler: true,
  // Day 2 turns this on.
  cacheComponents: false,
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
