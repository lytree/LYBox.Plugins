/**
 * 宿主 WebView 全局类型声明（单一真相源）。
 *
 * lybox.dev.ts 与 lybox.prod.ts 都消费这里的声明：
 * - dev 期 vite 同时加载两者，若各自 declare global 会触发 TS2717（同名属性类型须一致）；
 * - prod 期 vite alias 把 dev 重写为 prod，dev 文件根本不参与编译，
 *   故这里必须由两者显式 import type 才能在各自编译上下文中生效。
 *
 * `import type` 在编译期完全擦除，不产生任何运行时代码或产物字节。
 *
 * 完整契约见宿主 /sdk/lybox-plugin-sdk.d.ts。
 */

/** 宿主注入的 WebView 原生桥（window.__lybox），由 WebPluginView 导航完成后注入 ipc.js 建立。 */
export interface LyboxNativeBridge {
  invoke<T = unknown>(method: string, payload?: unknown): Promise<T>;
  invokeLegacy<T = unknown>(method: string, ...args: unknown[]): Promise<T>;
  rpc(...args: unknown[]): Promise<unknown>;
  on(event: string, cb: (data: unknown) => void): () => void;
  off(event: string, cb?: (data: unknown) => void): void;
  isWebView(): boolean;
}

/** 宿主嵌入式 SDK（window.LyboxPlugin），来自 /sdk/lybox-plugin-sdk.js。 */
export interface LyboxEmbeddedSdk {
  invoke: <TReq, TRes>(method: string, payload: TReq) => Promise<TRes>;
  invokeLegacy: <T = unknown>(method: string, ...args: unknown[]) => Promise<T>;
  on: <T = unknown>(event: string, cb: (data: T) => void) => () => void;
  off: (event: string, cb?: (data: unknown) => void) => void;
  isWebView(): boolean;
}

declare global {
  interface Window {
    __lybox?: LyboxNativeBridge;
    LyboxPlugin?: LyboxEmbeddedSdk;
  }
}
