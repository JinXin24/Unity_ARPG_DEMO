# Cinemachine 使用规范

**Virtual Camera 的 Follow 目标旋转会影响相机旋转：** 把 Body 组件的 **Binding Mode** 设成 **Lock To Target On Assign**——只在绑定那一刻取目标位置和朝向，之后目标旋转不会拖拽相机。适合"跟着角色走但不跟着角色转身"的场景。

