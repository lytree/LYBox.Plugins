/**
 * LYBox.Plugin.ViteSample — Vite dev 形态适配层。
 *
 * @version 2.3.0-preview.7   ← 与 LYBox/version.props 同步（CI 替换）。
 *
 * 本文件实现：Vite dev server 经同源代理访问宿主 WebHostService；
 * 生产构建由 vite alias 整体替换为 ./lybox.prod.ts，运行时透传宿主 window.LyboxPlugin。
 *
 * 共享契约：所有方法签名必须与 LYBox.Plugin.Shared.Web/Assets/lybox-plugin-sdk.js 一一对应。
 */

declare const __LYBOX_PLUGIN_ID__: string;

export const SDK_VERSION = '2.3.0-preview.7';

/** 当前插件 ID（与 csproj <PluginId> 一致）。 */
export const PLUGIN_ID = __LYBOX_PLUGIN_ID__;

const TOKEN_KEY = `lybox-dev-session:${PLUGIN_ID}`;

// —— 类型契约（与 /sdk/lybox-plugin-sdk.d.ts 一一对应）——

export interface RpcInvokeOptions {
    signal?: AbortSignal;
    timeout?: number;
}

export interface LyboxBridge {
    invoke(method: string, payload?: unknown, options?: RpcInvokeOptions): Promise<unknown>;
    invokeLegacy(method: string, ...args: unknown[]): Promise<unknown>;
    rpc(method: string, ...args: unknown[]): Promise<unknown>;
    on(event: string, cb: (data: unknown) => void): () => void;
    off(event: string, cb?: (data: unknown) => void): void;
    emit(event: string, data?: unknown): void;
    isWebView(): boolean;
    configureRuntime?(pluginId: string, sessionToken?: string): void;
    startSse?(pluginId: string, sessionToken?: string): void;
}

declare global {
    interface Window {
        __lybox?: LyboxBridge;
    }
}

/** 当前是否运行在宿主 WebView 内（window.__lybox 存在）。 */
export function isWebView(): boolean {
    return typeof window !== 'undefined' && typeof window.__lybox?.rpc === 'function';
}

/** 兼容别名。 */
export function isLyboxBridgeAvailable(): boolean {
    return isWebView();
}

/** 获取 Vite 形态的调试会话 token。 */
async function getDevSession(): Promise<string> {
    if (isWebView()) return '';

    let token = sessionStorage.getItem(TOKEN_KEY);
    if (token) return token;

    const resp = await fetch(`/__lybox/debug/session/${encodeURIComponent(PLUGIN_ID)}`, { method: 'POST' });
    if (!resp.ok) {
        throw new Error(`调试会话签发失败（HTTP ${resp.status}）。请确认宿主以 Debug 构建运行且 ${PLUGIN_ID} 已注册。`);
    }
    token = (await resp.json()).session as string;
    sessionStorage.setItem(TOKEN_KEY, token);
    return token;
}

/** Canonical 单 payload 调用。 */
export async function invoke<T = unknown>(
    method: string,
    payload?: unknown,
    options?: RpcInvokeOptions,
): Promise<T> {
    if (isWebView()) {
        return (await window.__lybox!.invoke(method, payload, options)) as T;
    }

    const token = await getDevSession();
    const resp = await fetch(`/__bridge/${encodeURIComponent(PLUGIN_ID)}/rpc`, {
        method: 'POST',
        headers: { 'content-type': 'application/json', 'X-LYBox-Session': token },
        body: JSON.stringify({
            version: 2,
            kind: 'plugin-rpc-call',
            pluginId: PLUGIN_ID,
            method,
            payload: payload ?? null,
        }),
        ...(options?.signal ? { signal: options.signal } : {}),
    });
    const env = (await resp.json()) as { payload?: T; error?: unknown };
    if (!resp.ok || env.error !== undefined) {
        throw new Error(`RPC '${method}' 失败: ${JSON.stringify(env.error ?? resp.status)}`);
    }
    return env.payload as T;
}

/** 兼容旧版多参调用。 */
export async function invokeLegacy<T = unknown>(method: string, ...args: unknown[]): Promise<T> {
    if (isWebView() && typeof window.__lybox?.invokeLegacy === 'function') {
        return (await window.__lybox.invokeLegacy(method, ...args)) as T;
    }
    const token = await getDevSession();
    const resp = await fetch(`/__bridge/${encodeURIComponent(PLUGIN_ID)}/rpc`, {
        method: 'POST',
        headers: { 'content-type': 'application/json', 'X-LYBox-Session': token },
        body: JSON.stringify({ name: method, args }),
    });
    const env = (await resp.json()) as { result?: T; error?: unknown };
    if (!resp.ok || env.error !== undefined) {
        throw new Error(`RPC '${method}' 失败: ${JSON.stringify(env.error ?? resp.status)}`);
    }
    return env.result as T;
}

/** 创建一个最简 transport，可直接喂给生成器的 createXxxClient。 */
export function createLyboxClient(): {
    invoke: <TReq, TRes>(method: string, payload: TReq, options?: RpcInvokeOptions) => Promise<TRes>;
    invokeLegacy: (method: string, ...args: unknown[]) => Promise<unknown>;
} {
    return {
        invoke: <TReq, TRes>(method: string, payload: TReq, options?: RpcInvokeOptions) =>
            invoke<TRes>(method, payload, options),
        invokeLegacy: (method: string, ...args: unknown[]) => invokeLegacy(method, ...args),
    };
}

/** 订阅宿主事件。 */
export function on<T = unknown>(event: string, cb: (data: T) => void): () => void {
    const handler = cb as (data: unknown) => void;
    if (isWebView()) return window.__lybox!.on(event, handler);

    // Vite 形态：单条 SSE + dispatch 信封
    const listeners = emitterListeners.get(event) ?? [];
    listeners.push(handler);
    emitterListeners.set(event, listeners);
    void ensureSse();
    return () => {
        const set = emitterListeners.get(event);
        if (!set) return;
        const i = set.indexOf(handler);
        if (i >= 0) set.splice(i, 1);
        if (set.length === 0) emitterListeners.delete(event);
    };
}

/** 取消事件订阅。 */
export function off(event: string, cb?: (data: unknown) => void): void {
    if (isWebView()) {
        window.__lybox?.off(event, cb);
        return;
    }
    const set = emitterListeners.get(event);
    if (!set) return;
    if (cb) {
        const i = set.indexOf(cb);
        if (i >= 0) set.splice(i, 1);
    } else {
        set.length = 0;
    }
    if (set.length === 0) emitterListeners.delete(event);
}

// —— SSE 单连接管理（Vite 形态）——

const emitterListeners = new Map<string, Array<(data: unknown) => void>>();
let ssePromise: Promise<EventSource> | undefined;

async function ensureSse(): Promise<EventSource> {
    if (ssePromise) return ssePromise;
    ssePromise = (async () => {
        const token = await getDevSession();
        const es = new EventSource(`/sse/${encodeURIComponent(PLUGIN_ID)}?session=${encodeURIComponent(token)}`);
        es.addEventListener('dispatch', (e) => {
            const msg = JSON.parse((e as MessageEvent).data) as { name: string; data: unknown };
            emitterListeners.get(msg.name)?.forEach(cb => cb(msg.data));
        });
        return es;
    })();
    return ssePromise;
}