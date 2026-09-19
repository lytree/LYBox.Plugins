/**
 * LYBox.Plugin.ViteSample — lybox.ts 适配层入口。
 *
 * 形态切换：
 *  - Vite dev  : src/lybox.dev.ts（HTTP 桥接 + SSE；完整 HMR；IDE 提示来自本文件）
 *  - WebView   : src/lybox.prod.ts（透传宿主 window.LyboxPlugin；dist/*.js 不含 SDK 字节）
 *
 * 业务代码统一 `import { ... } from './lybox'`，由 vite alias 在 dev/prod 间自动切换。
 *
 * @version 2.3.0-preview.7   ← 与 LYBox/version.props 同步（CI 替换）。
 */

export {
    SDK_VERSION,
    PLUGIN_ID,
    invoke,
    invokeLegacy,
    createLyboxClient,
    isWebView,
    isLyboxBridgeAvailable,
    on,
    off,
} from './lybox.dev';

export type { RpcInvokeOptions, LyboxBridge } from './lybox.dev';