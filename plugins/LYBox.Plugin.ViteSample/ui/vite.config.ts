import { defineConfig } from 'vite';
import { fileURLToPath } from 'node:url';

/**
 * LYBox.Plugin.ViteSample Vite 配置 —— 双形态适配：
 *
 *  dev  : Vite dev server (5173) → 同源代理到宿主 (58080)。HMR 全功能联调。
 *  prod : Vite build 产物 dist/ → 拷入插件 wwwroot/。运行时由宿主 /sdk/lybox-plugin-sdk.js 接管。
 *
 * @version 2.3.0-preview.7   ← 与 LYBox/version.props 同步（CI 替换）。
 */

const SDK_VERSION = '2.3.0-preview.7';

// 宿主 WebHostService 地址：与启动器侧 LYBOX_WEB_PORT 保持一致（默认 58080）。
// Vite 形态：浏览器视角所有请求都来自 5173，绕开宿主无 CORS 响应头的问题。
const hostPort = process.env.LYBOX_WEB_PORT ?? '58080';
const hostTarget = `http://127.0.0.1:${hostPort}`;

// 必须与 LYBox.Plugin.ViteSample.csproj 的 <PluginId> 一致。
const PLUGIN_ID = 'a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d';

export default defineConfig(({ mode }) => ({
    define: {
        __LYBOX_SDK_VERSION__: JSON.stringify(SDK_VERSION),
        __LYBOX_PLUGIN_ID__: JSON.stringify(PLUGIN_ID),
    },
    resolve: {
        alias: [
            // 生产构建：把 ./lybox.dev 重写为 ./lybox.prod，让 dist/*.js 不含 SDK 字节。
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
        outDir: 'dist',
        emptyOutDir: true,
        minify: 'esbuild',
        sourcemap: true,
    },
    server: {
        port: 5173,
        strictPort: true,
        proxy: {
            '/__bridge': { target: hostTarget, changeOrigin: false },
            '/sse': { target: hostTarget, changeOrigin: false },
            '/__lybox': { target: hostTarget, changeOrigin: false },
            // 宿主嵌入式 SDK（dev 期让 IDE 类型提示与浏览器共用一份资源）
            '/sdk': { target: hostTarget, changeOrigin: false },
        },
    },
}));