/**
 * LYBox 宿主桥接 —— Vite dev 形态。
 *
 * @version 2.3.0-preview.9   ← 与 LYBox.Plugins/version.props 的 <LyboxVersion> 同步。
 *
 * 浏览器打开 Vite dev server（5178）时，宿主 WebView 并不存在，
 * window.__lybox 也不会注入。此形态改为经同源代理走宿主的 HTTP RPC 桥：
 *   - RPC：POST /__bridge/{pluginId}/rpc
 *   - 会话：POST /__lybox/debug/session/{pluginId} 签发（宿主须为 Debug 构建）
 * 代理目标端口取自 LYBOX_WEB_PORT，与启动器共用。
 *
 * 生产构建由 vite alias 整体替换为 ./lybox.prod.ts。
 */

import type { LyboxNativeBridge } from './lybox-env';

declare const __LYBOX_PLUGIN_ID__: string;

export const PLUGIN_ID = __LYBOX_PLUGIN_ID__;

/** 是否运行在宿主 WebView 内（dev 形态下为 false，一律走 HTTP 桥）。 */
export function isWebView(): boolean {
  if (typeof window === 'undefined') return false;
  const bridge: LyboxNativeBridge | undefined = window.__lybox;
  return bridge !== undefined && typeof bridge.rpc === 'function';
}

const TOKEN_KEY = `lybox-dev-session:${PLUGIN_ID}`;

/** 取得调试会话 token（dev 专用，宿主 Release 构建下该端点不存在）。 */
async function getDevSession(): Promise<string> {
  if (isWebView()) return '';

  let token = sessionStorage.getItem(TOKEN_KEY);
  if (token) return token;

  const resp = await fetch(`/__lybox/debug/session/${encodeURIComponent(PLUGIN_ID)}`, {
    method: 'POST',
  });
  if (!resp.ok) {
    throw new Error(
      `调试会话签发失败（HTTP ${resp.status}）。请确认宿主以 Debug 构建运行，且插件 ${PLUGIN_ID} 已注册。`
    );
  }
  token = ((await resp.json()) as { session: string }).session;
  sessionStorage.setItem(TOKEN_KEY, token);
  return token;
}

/** 经 HTTP 桥调用宿主 [RpcCommand]。 */
export async function invoke<TRes = unknown>(method: string, payload: unknown): Promise<TRes> {
  if (isWebView()) {
    return (await window.__lybox!.invoke(method, payload)) as TRes;
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
  });

  const env = (await resp.json()) as { payload?: TRes; error?: unknown };
  if (!resp.ok || env.error !== undefined) {
    throw new Error(`RPC '${method}' 失败：${JSON.stringify(env.error ?? resp.status)}`);
  }
  return env.payload as TRes;
}
