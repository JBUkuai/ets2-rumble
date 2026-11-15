# 第三方组件与声明 / Third-Party Notices

本仓库自身代码以 MIT 许可发布（见 `LICENSE`）。以下是它依赖或交互的第三方项目与商标说明。

The code in this repository is MIT-licensed (see `LICENSE`). Below are the third-party projects and marks it interacts with.

---

## 1. scs-sdk-plugin（不随本仓库分发 / not redistributed）

- 项目 / Project: <https://github.com/RenCloud/scs-sdk-plugin>
- 上游 / Upstream: <https://github.com/nlhans/ets2-sdk-plugin>
- 许可 / License: MIT

该插件把 SCS Telemetry SDK 的数据写入 Windows 文件映射对象 `Local\SCSTelemetry`。本程序只**读取**该共享内存。

本仓库**不包含** `scs-telemetry.dll`，也不重新分发它。使用者需自行从上游获取，并遵守其许可。

The plugin writes SCS Telemetry SDK data into the file mapping object `Local\SCSTelemetry`. This program only **reads** that shared memory. The DLL is **not** included or redistributed here; obtain it from upstream and follow its license.

其许可证原文（MIT）要求在所有副本或实质性部分中保留以下版权与许可声明：

```
The MIT License (MIT)

Copyright (c) 2014 Hans

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## 2. SCS Software / Euro Truck Simulator 2

SCS Telemetry SDK、Euro Truck Simulator 2 及相关名称、标识、商标归 **SCS Software s.r.o.** 所有。

本程序是第三方玩家工具：

- 与 SCS Software **没有任何隶属、合作或背书关系**；
- 不使用 SCS 的官方标识，不暗示官方认可；
- 不修改、不重新分发游戏本体或 SDK 的任何文件。

SCS Telemetry SDK, Euro Truck Simulator 2 and related names and marks are property of **SCS Software s.r.o.** This is an unofficial third-party tool with no affiliation, partnership or endorsement. It does not use official SCS branding, does not modify the game, and does not redistribute game or SDK files.

---

## 3. Microsoft / .NET / XInput

- 本程序基于 .NET 8（MIT 许可）构建，使用 Windows 自带的 `xinput1_4.dll`（缺失时回退 `xinput1_3.dll` / `xinput9_1_0.dll`）。
- .NET 许可：<https://github.com/dotnet/runtime/blob/main/LICENSE.TXT>
- XInput 属于 Windows SDK 的一部分，本仓库不包含任何 Microsoft 二进制文件（发布版中的 .NET 运行时由 `dotnet publish --self-contained` 打包，遵循 .NET 运行时许可）。

Built on .NET 8 (MIT) and the Windows-provided XInput DLLs. No Microsoft binaries are vendored in this repository; the self-contained release bundles the .NET runtime under its own license.

---

## 4. 商标与免责 / Trademarks and disclaimer

所有第三方商标归其各自所有者。本项目按"现状"提供，不附带任何明示或暗示担保；使用本程序造成的任何后果由使用者自行承担。

All third-party trademarks belong to their respective owners. This project is provided "as is", without warranty of any kind. Use at your own risk.
