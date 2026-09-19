/**
 * LYBox.Plugin.ViteSample 前端入口。
 *
 * @version 2.3.0-preview.7   ← 与 LYBox/version.props 同步（CI 替换）。
 *
 * 演示：环境检测 + 强类型 RPC + SSE 事件订阅。
 */

import { isWebView, PLUGIN_ID, SDK_VERSION, on } from './lybox';
import { samples } from './api/samples';

declare const __LYBOX_SDK_VERSION__: string;

document.getElementById('env')!.textContent = `运行形态：${isWebView() ? 'WebView' : 'Vite/浏览器'}（plugin=${PLUGIN_ID}）`;
document.getElementById('ver')!.textContent = `SDK 版本：编译期 ${__LYBOX_SDK_VERSION__} · 运行期 ${SDK_VERSION}`;

// —— RPC 演示：强类型客户端 ——
document.getElementById('btn-greet')!.addEventListener('click', async () => {
    const name = (document.getElementById('name') as HTMLInputElement).value;
    const el = document.getElementById('greet-result')!;
    try {
        const result = await samples.greet({ name });
        el.textContent = `✓ ${result}`;
    } catch (e) {
        el.textContent = `✗ ${(e as Error).message ?? String(e)}`;
    }
});

document.getElementById('btn-add')!.addEventListener('click', async () => {
    const left = Number((document.getElementById('a') as HTMLInputElement).value);
    const right = Number((document.getElementById('b') as HTMLInputElement).value);
    const el = document.getElementById('add-result')!;
    try {
        const result = await samples.add({ left, right });
        el.textContent = `${left} + ${right} = ${result}`;
    } catch (e) {
        el.textContent = `✗ ${(e as Error).message ?? String(e)}`;
    }
});

document.getElementById('btn-info')!.addEventListener('click', async () => {
    const el = document.getElementById('info-result')!;
    try {
        const result = await samples.getSampleInfo();
        el.textContent = JSON.stringify(result, null, 2);
    } catch (e) {
        el.textContent = `✗ ${(e as Error).message ?? String(e)}`;
    }
});

// —— 事件演示：SSE/WebView 通道 ——
const eventsList = document.getElementById('events')!;
on<{ count: number; time: string; message: string }>('tick', data => {
    const li = document.createElement('li');
    li.textContent = `[${data.time}] #${data.count} ${data.message}`;
    eventsList.appendChild(li);
});