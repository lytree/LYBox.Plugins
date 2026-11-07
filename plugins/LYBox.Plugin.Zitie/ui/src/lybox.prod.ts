/**
 * LYBox 宿主桥接 —— 生产构建形态（仅 vite build 引用）。
 *
 * @version 2.4.0-beta.1   ← 与 LYBox.Plugins/version.props 的 <LyboxVersion> 同步。
 *
 * 由 vite alias 在 production 模式下把 ./lybox.dev 重写为本文件。
 * 所有调用委托给宿主嵌入式 SDK（window.LyboxPlugin，
 * 来自 index.html 中的 <script src="/sdk/lybox-plugin-sdk.js">）。
 * dist/*.js 中不含 lybox.dev 的实现字节。
 */

import type { LyboxEmbeddedSdk, LyboxNativeBridge } from './lybox-env';

declare const __LYBOX_PLUGIN_ID__: string;

export const PLUGIN_ID = __LYBOX_PLUGIN_ID__;

/** 是否运行在宿主 WebView 内。宿主注入 window.__lybox.rpc。 */
export function isWebView(): boolean {
  return typeof window !== 'undefined' && typeof window.__lybox?.rpc === 'function';
}

/**
 * 用宿主原生桥（window.__lybox，由 WebView2 导航完成后注入的 ipc.js 建立）
 * 拼出与嵌入式 SDK 同形的接口，作为 SDK 未挂载时的兜底。
 *
 * 两条通路最终都落到宿主同一套 IPC：嵌入式 SDK（/sdk/lybox-plugin-sdk.js）本身
 * 在 WebView 模式下也是转发给 window.__lybox，故兜底不改变语义，只去掉对
 * SDK script 成功加载的硬依赖。
 */
function nativeBridgeFallback(): LyboxEmbeddedSdk | null {
  const bridge: LyboxNativeBridge | undefined = window.__lybox;
  if (!bridge || typeof bridge.invoke !== 'function') return null;

  return {
    invoke: <TReq, TRes>(method: string, payload: TReq) =>
      bridge.invoke<TRes>(method, payload),
    invokeLegacy: <T = unknown>(method: string, ...args: unknown[]) =>
      bridge.invokeLegacy<T>(method, ...args),
    on: <T = unknown>(event: string, cb: (data: T) => void) =>
      bridge.on(event, cb as (data: unknown) => void),
    off: (event: string, cb?: (data: unknown) => void) => bridge.off(event, cb),
    isWebView: () => bridge.isWebView(),
  };
}

/**
 * 取宿主调用入口：优先嵌入式 SDK，其次原生桥；
 * 两者都不可用时抛出可定位的错误。
 * window.__lybox / window.LyboxPlugin 的全局类型声明统一在 lybox.dev.ts（TS2717 要求单一定义）。
 */
function getApi(): LyboxEmbeddedSdk {
  const api = window.LyboxPlugin;
  if (api) return api;

  const fallback = nativeBridgeFallback();
  if (fallback) return fallback;

  throw new Error(
    'LYBox SDK 未挂载：请确认 index.html 已注入 /sdk/lybox-plugin-sdk.js，且当前运行在宿主 WebView 内。'
  );
}

/** 调用宿主 [RpcCommand]。 */
export async function invoke<TRes = unknown>(method: string, payload: unknown): Promise<TRes> {
  return getApi().invoke<unknown, TRes>(method, payload);
}
