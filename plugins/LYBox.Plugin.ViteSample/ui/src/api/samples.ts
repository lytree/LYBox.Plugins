/**
 * 强类型 RPC 客户端：消费源生成器产出的 SampleCommands.client.d.ts。
 *
 * 路径 ../wwwroot/.lybox/SampleCommands.client.d.ts 由 LYBox.Plugin.Generators 源生成器在构建期产出，
 * 并由 LYBox.Plugin.Shared.Web.targets 的 IncludeWebPluginAssets 目标拷入 wwwroot/.lybox/。
 *
 * IDE 提示完整：samples.greet({ name }) / samples.add({ left, right }) / samples.getSampleInfo() 全程强类型。
 *
 * 注意：本文件 dev 期需要 ../wwwroot/.lybox/ 目录存在（生成器产物）。若未生成，
 *       `pnpm build` 一次插件工程后即会出现在 bin/Debug/.../wwwroot/.lybox/，开发者可选择性软链或拷贝。
 */

import { createSampleCommandsClient } from '../../../wwwroot/.lybox/SampleCommands.client';
import { createLyboxClient } from '../lybox';

const transport = createLyboxClient();
export const samples = createSampleCommandsClient(transport);