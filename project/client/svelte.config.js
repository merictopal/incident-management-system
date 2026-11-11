import adapter from '@sveltejs/adapter-node';
import { vitePreprocess } from '@sveltejs/vite-plugin-svelte';

/** @type {import('@sveltejs/kit').Config} */
export default {
  preprocess: vitePreprocess(),
  // adapter-node produces a small production Node server (used by the Dockerfile).
  kit: { adapter: adapter() }
};
