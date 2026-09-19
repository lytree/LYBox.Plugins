/**
 * LYBox.Plugin.ViteSample — 生产构建形态适配层（仅 vite build 引用）。
 *
 * @version 2.3.0-preview.7   ← 与 LYBox/version.props 同步（CI 替换）。
 *
 * 由 vite alias 在 production 模式下把 ./lybox.dev 重写为本文件。
 * 所有运行时方法委托给宿主嵌入式 SDK（window.LyboxPlugin 来自 /sdk/lybox-plugin-sdk.js）。
 * dist/*.js 中不含 lybox.dev / lybox.prod 的实现字节；SDK 由宿主按需加载。
 */

declare const __LYBOX_PLUGIN_ID__: string;

export const SDK_VERSION = '2.3.0-preview.7';
export const PLUGIN_ID = __LYBOX_PLUGIN_ID__;

export interface RpcInvokeOptions {
    signal?: AbortSignal;
    timeout?: number;
}

interface LyboxPublicApi {
    invoke: <TReq, TRes>(method: string, payload: TReq, options?: RpcInvokeOptions) => Promise<TRes>;
    invokeLegacy: (method: string, ...args: unknown[]) => Promise<unknown>;
    on: (event: string, cb: (data: unknown) => void) => () => void;
    off: (event: string, cb?: (data: unknown) => void) => void;
    emit?: (event: string, data?: unknown) => void;
    isWebView?: () => boolean;
}

declare global {
    interface Window {
        __lybox?: { rpc: (...args: unknown[]) => Promise<unknown> };
        LyboxPlugin?: LyboxPublicApi;
    }
}

function getApi(): LyboxPublicApi {
    if (typeof window === 'undefined' || !window.LyboxPlugin) {
        throw new Error('LYBox SDK 未挂载；生产构建需在 HTML 头部加 <script src="/sdk/lybox-plugin-sdk.js">。');
    }
    return window.LyboxPlugin;
}

export function isWebView(): boolean {
    return typeof window !== 'undefined' && typeof window.__lybox?.rpc === 'function';
}

export function isLyboxBridgeAvailable(): boolean {
    return isWebView();
}

export async function invoke<T = unknown>(
    method: string,
    payload?: unknown,
    options?: RpcInvokeOptions,
): Promise<T> {
    return getApi().invoke(method, payload, options) as Promise<T>;
}

export async function invokeLegacy<T = unknown>(method: string, ...args: unknown[]): Promise<T> {
    return getApi().invokeLegacy(method, ...args) as Promise<T>;
}

export function createLyboxClient(): {
    invoke: <TReq, TRes>(method: string, payload: TReq, options?: RpcInvokeOptions) => Promise<TRes>;
    invokeLegacy: (method: string, ...args: unknown[]) => Promise<unknown>;
} {
    return {
        invoke: <TReq, TRes>(method: string, payload: TReq, options?: RpcInvokeOptions) =>
            getApi().invoke(method, payload, options),
        invokeLegacy: (method: string, ...args: unknown[]) =>
            getApi().invokeLegacy(method, ...args),
    };
}

export function on<T = unknown>(event: string, cb: (data: T) => void): () => void {
    return getApi().on(event, cb as (data: unknown) => void);
}

export function off(event: string, cb?: (data: unknown) => void): void {
    const api = getApi();
    if (typeof api.off === 'function') api.off(event, cb);
}