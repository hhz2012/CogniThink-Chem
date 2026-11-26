# 拍照解题并讲解 实现计划

## 一、Repository Research（现状与缺口）

### 现有能力（已就绪，可复用）
- **视觉 LLM 通道已存在**：`LlmProxyService.ChatWithImagesAsync`（[LlmProxyService.cs:88](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Agent/LlmProxyService.cs#L88)）已会把图片转 base64 按 OpenAI 兼容格式发给模型；`SupportsVision` 按模型名含 `vl` 判定。
- **网关已支持图片**：`LlmGatewayService.ProcessStudentMessageAsync(rawText, imagePaths, ...)` 在解析阶段（[LlmGatewayService.cs:151](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L151)）会把配图随 prompt 发给视觉模型识别题目。
- **真实题图素材现成**：`src/CogniThink.TutorBrain.Api/data/pdf_images/` 下有 200+ 张从真实高考/习题 PDF 裁出的题目图片，是天然的"真实高中化学题"测试集。
- **前端解题页已成型**：[solve/index.tsx](file:///d:/github/CogniThink-Chem/miniapp/src/pages/solve/index.tsx) 文本输入 → `tutorApi.solve(text)` → 渲染 `StudentSolveView`（结论卡 / 讲解 / 推理 DAG / 微观解释）。
- **CORS 已全开**（Program.cs L93-94），前端跨域上传无障碍。

### 已验证的关键事实（本会话实测）
- `appsettings.Development.json` 里的 key + 中转 URL `maas.qianwenaiapi.com` **已失效**（官方端点返回 `invalid_api_key`，纯文本与视觉均 401）。
- 系统环境变量 **`QWEN_API_KEY` 是有效 key**：对官方端点 `https://dashscope.aliyuncs.com/compatible-mode/v1` 纯文本调用 200 OK。
- 视觉模型 **`qwen3-vl-plus`** 在官方端点可用（喂真实题图返回了内容）；`qwen-vl-plus`/`qwen2-vl-plus` 等会 `model_not_found`。
- 结论：运行后端时需用 **官方端点 + 环境变量里的活 key + `qwen3-vl-plus`**。不把活 key 写进仓库文件。

### 缺口
1. **后端无"接收用户上传图片"的端点**：`ChatWithImagesAsync` 只从磁盘 `data/<rel>` 读图，无法接收前端上传的字节流。
2. **前端无拍照/上传入口**：solve 页只有文本框。
3. **配置指向失效中转**：`appsettings.Development.json` 的 BaseUrl/ApiKey 已过期。

## 二、范围与红线
- **范围**：新增"拍照/上传图片 → OCR 抽题干 → 解题并讲解"端到端能力，先打通可演示的 MVP。
- **红线**（沿用项目记忆）：
  - 零回归：不改现有 `/api/gateway/solve` 行为与确定性引擎管线；新增端点为加法式接线。
  - 活 key 不入库：只通过环境变量 `QWEN_API_KEY` / `Llm__ApiKey` 注入。
  - OCR 结果可编辑：不让 OCR 噪声直接污染求解，用户确认/修改后再解。
  - 提交前必须 `dotnet build` + `tsc --noEmit` 通过，并在浏览器实测截图。

## 三、Files and Modules

### 后端
- `src/CogniThink.TutorBrain.Api/Application/Agent/LlmProxyService.cs`：新增 `ChatWithImageBytesAsync(system, user, images, ...)`，`images` 为 `IReadOnlyList<(string Mime, byte[] Data)>`；抽取现有 base64 拼装逻辑为私有 helper 复用。原 `ChatWithImagesAsync`（磁盘路径版）保留不动（零回归）。
- `src/CogniThink.TutorBrain.Api/Api/Controllers/GatewayController.cs`：新增两个端点：
  - `POST /api/gateway/ocr-photo`：接收 `IFormFile`（或 JSON base64）→ 调视觉 LLM 抽题干 → 返回 `{ text: string }`。
  - `POST /api/gateway/solve-photo`：OCR 抽题干后直接 `ProcessStudentMessageAsync(extractedText)` → `GatewaySolveProjector.Project` 返回 `StudentSolveView`（一键拍照解题）。
- `src/CogniThink.TutorBrain.Api/Program.cs`：配置绑定处加一个回退——若 `Llm:ApiKey` 为空，读取环境变量 `QWEN_API_KEY`（使本地开箱即用，无需改文件）。
- `src/CogniThink.TutorBrain.Api/appsettings.Development.json`：`BaseUrl` 改为官方端点 `https://dashscope.aliyuncs.com/compatible-mode/v1`（非密钥，可入库）；`ApiKey` 留空（由环境变量注入）；`Model` 保持 `qwen3-vl-plus`。

### 前端
- `miniapp/src/services/tutorApi.ts`：新增 `ocrPhoto(tempFilePath)`（`Taro.uploadFile` multipart 上传）和 `solvePhoto(tempFilePath)`，返回 OCR 文本 / `StudentSolveView`。
- `miniapp/src/pages/solve/index.tsx`：在输入区加"📷 拍照/相册"按钮 → `Taro.chooseImage` 选图 → 预览缩略图 → 调 `ocrPhoto` → 把抽出的文本回填到可编辑文本框 → 用户点"开始求解"走原有流程；另加一个"拍照直接求解"快捷按钮调 `solvePhoto`。
- `miniapp/src/pages/solve/index.module.scss`：加预览图、按钮行的样式（沿用现有 token/px 约定）。

## 四、Implementation Steps（依赖序）
1. **后端：LlmProxyService 加字节版视觉调用**——抽取 base64 拼装 helper，新增 `ChatWithImageBytesAsync`；不删原磁盘版。
2. **后端：Program.cs 配置回退 + appsettings.Development.json 修 BaseUrl**——让官方端点 + 环境变量 key 生效。
3. **后端：GatewayController 加 `ocr-photo` / `solve-photo` 端点**——OCR prompt 固定为"准确提取题目文本，原样输出，不解题"；multipart 接收，限制大小（如 ≤10MB）与图片类型。
4. **后端：`dotnet build` 0 错误**；启动后端（带 `Llm__*` 环境变量覆盖）→ curl 一张 `data/pdf_images` 真实图验证 OCR 端点返回题干。
5. **前端：tutorApi 加两个方法 + solve 页加拍照 UI**——`chooseImage` 双源（camera/album），预览，回填可编辑文本，一键求解。
6. **前端：`tsc --noEmit` 0 错误**。
7. **端到端实测**：miniapp dev server + 后端同时跑，浏览器上传 2~3 张不同题型真实题图（选择/填空/计算），截图 OCR 文本 + 求解讲解效果。
8. **网上真题素材（可选加分）**：从已搜到的 2025 高考真题来源尝试再取 1 张新图测试；若站点防爬则以仓库内 pdf_images 为准（本身即真实高考题）。

## 五、Dependencies and Considerations
- 运行时依赖：环境变量 `QWEN_API_KEY` 必须在启动后端的 shell 中存在（本 Trae 会话已存在）；若在别处运行需 `$env:Llm__ApiKey=$env:QWEN_API_KEY` 或直接设 `Llm__ApiKey`。
- `Taro.uploadFile` 在 H5 端走浏览器 multipart，与后端 `IFormFile` 兼容；微信小程序端需配置合法域名（本次仅 H5 演示）。
- 视觉模型 30s 超时：`appsettings.Development.json` 已设 30s；复杂题可适当放宽，演示阶段足够。
- 化学公式/角标 OCR：`qwen3-vl-plus` 对中文+公式识别较强，但仍可能有错 → 提供"可编辑文本框"是关键护栏。

## 六、Validation
- `dotnet build src/CogniThink.TutorBrain.Api -c Debug`：0 错误。
- 后端启动后 `curl -F "file=@xxx.png" http://localhost:5080/api/gateway/ocr-photo`：返回非空题干文本。
- `npx.cmd tsc --noEmit`（miniapp）：0 错误。
- 浏览器：上传真实题图 → 看到 OCR 文本回填 → 求解出结论卡 + 讲解；截图存档；无水平溢出、无控制台红错。
- 回归：原 `/api/gateway/solve` 文本求解行为不变（抽测一题文本仍正常）。

## 七、Risks
- **活 key 仅在当前环境变量**：换机器/会话会失效 → 不硬编码；文档注明从环境变量注入；演示在此会话完成。
- **OCR 准确率**：拍照歪斜/模糊会抽错 → 前端可编辑 + 提示用户核对；不在本次追求 100% OCR。
- **后端启动慢/构建问题**：先 build 验证；若 EF 迁移/种子数据拖慢，仅启动 API 项目即可（知识库从 JSON 加载，不需外网）。
- **一键 solve-photo 跳过人工核对**：OCR 错会导致求解错 → 主推"OCR→编辑→求解"两阶段；solve-photo 标注为"快速尝鲜，建议核对文本"。
- **范围蔓延**：不做题库改造、不做小程序端适配、不做多图拼接，仅 H5 单图 MVP。
