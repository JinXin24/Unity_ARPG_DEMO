---
name: camera-keyframe-calculation
description: Scene相机参数→CameraStateSO yaw/pitch/armLength 计算工作流
metadata:
  type: project
---

用户配置 CameraStateSO 镜头参数的工作流：

1. Scene 视图摆好镜头 → Ctrl+Shift+P 打印位置和旋转
2. 用户报：相机位置、相机旋转（特写镜头才需要）、枢轴挂点位置
3. 反算 yaw/pitch/armLength：
   - armLength = 相机到挂点的欧几里得距离
   - yaw = Atan2(dx, dz) × Rad2Deg（水平角）
   - pitch = Asin(dy / armLength) × Rad2Deg（俯仰角）
4. 特写镜头（相机不看向角色）：算 lookAt 空物品位置 = 相机位置 + forward × 10m

**Why:** 每次配镜头都要手工算，用这个模板丢给 AI 秒出结果。
**How to apply:** 新会话直接粘贴用户准备好的提示词模板，或用户报数值后直接用公式算。**已升级为正式 skill：`/camera-calc`（.claude/skills/camera-calc/SKILL.md），可直接调用。**

相关文件：[[SceneCameraPrinter]] [[CameraStateSO]] [[CameraController]]
