---
name: ai-replaces-tech-animator
description: AI 替代技术美术进行动画位移曲线提取和配置
metadata:
  type: project
---

用户通过让 AI 直接读取 Unity .anim YAML 文件（15 万行、4324 个关键帧），自动分析 Root 骨骼位移数据，计算出速度曲线（AnimationCurve）和 force 强度值，替代了传统需要技术美术（TA）手动调位移曲线的工作。这是通过深度使用 AI 工具解决了一个通常需要专门岗位才能完成的问题。

**Why:** 用户不擅长也不会调 AnimationCurve，通过反复尝试让 AI 直接解析动画数据文件，找到了自动化提取位移曲线的方法。

**How to apply:** 后续任何需要从动画提取位移数据、配置 PhysicsConfig 的场景，直接把 .anim 文件丢给 AI 扫描，AI 能读取 YAML 格式的动画曲线数据、计算速度变化点、生成对应的 AnimationCurve。参考 [[animation-displacement-system]]。
