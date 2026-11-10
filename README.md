<<<<<<< HEAD
# PCL-Resurrection-Edition
=======
# PCL-Resurrection-Edition

基于 [PCL2（Plain Craft Launcher 2）开源恢复版 OpenPCL](https://github.com/CortexaX/OpenPCL) 的二次开发版启动器。

## 新增功能

- **皮肤制作**：导入 64×64（或旧版 64×32 自动转换）皮肤，像素编辑（画笔/橡皮/取色/调色板/撤销），Steve（经典臂）/ Alex（纤细臂）3D 实时预览，一键导出 PNG
- **启动前 Mod 检测**：启动时自动检测与游戏版本不匹配的 Mod 并从 Modrinth 拉取匹配版本替换，缺失前置自动补齐，检测结果与修正过程实时显示在启动页
- **黑暗模式**：完整的深色主题
- **Java 下载**：内置多版本 Java 下载安装
- **数据目录 PCLSE**：启动器数据文件夹更名为 `PCLSE`，与原版 PCL 互不干扰

## 构建方式

使用 Visual Studio 2022 打开 `Plain Craft Launcher 2.sln`，或直接运行：

```powershell
.\build-local.ps1
```

脚本会自动还原 .NET Framework 4.8 构建环境并编译，产物为单文件 exe。

## 许可

遵循原版 PCL2 与上游 OpenPCL（MIT）的许可协议，版权归原作者所有。二次分发请保留原作者信息。
>>>>>>> 3006d9b (PCL Resurrection Edition：基于 PCL2 开源恢复版二次开发)
