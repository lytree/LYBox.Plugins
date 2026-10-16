import { defineConfig } from 'vite';
import vueJsx from '@vitejs/plugin-vue-jsx';
import { fileURLToPath, URL } from 'node:url';

/**
 * LYBox.Plugin.Zitie 前端构建配置 —— 双形态适配：
 *
 *  dev  : Vite dev server (5178) → 同源代理到宿主 (LYBOX_WEB_PORT)。
 *         浏览器打开即可联调宿主 RPC（HMR 全功能）。
 *  prod : Vite build 产物 dist/ → 拷入插件 wwwroot/。
 *         运行时由宿主 /sdk/lybox-plugin-sdk.js 接管。
 *
 * @version 2.3.0-preview.9   ← 与 LYBox.Plugins/version.props 同步。
 */

const SDK_VERSION = '2.3.0-preview.9';

// 必须与 LYBox.Plugin.Zitie.csproj 的 <PluginId> 一致。
const PLUGIN_ID = '5e3f7a21-9c48-4d6b-b1f2-7a8e3c4d5b60';

// 宿主 WebHostService 地址：与启动器侧 LYBOX_WEB_PORT 保持一致。
const hostPort = process.env.LYBOX_WEB_PORT ?? '58080';
const hostTarget = `http://127.0.0.1:${hostPort}`;

export default defineConfig(({ mode }) => ({
  // 相对基址：宿主以 /{pluginId}/ 前缀挂载 wwwroot，绝对路径会 404。
  base: './',
  define: {
    __LYBOX_SDK_VERSION__: JSON.stringify(SDK_VERSION),
    __LYBOX_PLUGIN_ID__: JSON.stringify(PLUGIN_ID),
  },
  plugins: [vueJsx()],
  resolve: {
    alias: [
      {
        find: '@',
        replacement: fileURLToPath(new URL('./src', import.meta.url)),
      },
      // 生产构建：把 ./lybox.dev 重写为 ./lybox.prod，让 dist/*.js 不含 dev 桥接字节。
      ...(mode === 'production'
        ? [
            {
              find: /^\.\/lybox\.dev$/,
              replacement: fileURLToPath(new URL('./src/lybox.prod.ts', import.meta.url)),
            },
            {
              find: /.*\/src\/lybox\.dev$/,
              replacement: fileURLToPath(new URL('./src/lybox.prod.ts', import.meta.url)),
            },
          ]
        : []),
    ],
  },
  build: {
    target: 'es2020',
    outDir: 'dist',
    emptyOutDir: true,
    assetsInlineLimit: 0,
  },
  server: {
    port: 5178,
    strictPort: true,
    host: '127.0.0.1',
    proxy: {
      // 宿主 RPC HTTP 桥 / SSE / 调试会话 / 嵌入式 SDK
      '/__bridge': { target: hostTarget, changeOrigin: false },
      '/sse': { target: hostTarget, changeOrigin: false },
      '/__lybox': { target: hostTarget, changeOrigin: false },
      '/sdk': { target: hostTarget, changeOrigin: false },
    },
  },
}));
