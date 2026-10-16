/**
 * LYBox 宿主桥接统一出口。
 *
 * 实现体由 vite alias 按 mode 切换：
 *   - dev  → ./lybox.dev  （经同源代理走 HTTP RPC 桥）
 *   - prod → ./lybox.prod （透传宿主 window.LyboxPlugin）
 *
 * 业务代码只从本模块导入，不直接引用 dev/prod 具体实现。
 */
export { invoke, isWebView, PLUGIN_ID } from './lybox.dev';
