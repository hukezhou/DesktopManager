# 桌面管理器（Desktop Manager）

模仿微信桌面版界面风格的桌面快捷方式管理器。C# / WPF / .NET 8，MVVM 分层，前后端解耦。

## 运行

```powershell
dotnet build DesktopManager.sln
dotnet run --project src/DesktopManager
```

或直接用 Visual Studio 打开 `DesktopManager.sln`。

## 界面结构

```
┌──────────────────────────────────────────────────────────┐
│ 标题栏（左侧狐标识）：置顶 最小化 最大化 关闭             │
├────┬─────────────────────────────────────────────────────┤
│    │ 搜索框                                    ⊕ 添加     │
│侧  ├─────────────────────────────────────────────────────┤
│边  │ 图标  名称                                          │
│栏  │ ─────────────────────────────────────────────────── │
│60px│ 图标  名称                                          │
└────┴─────────────────────────────────────────────────────┘
```

- **标题栏**：无边框窗口 + `WindowChrome`，左侧不显示任何文字，只有一个应用标识——`Image` 直接引用 `Assets/app.ico`（与 exe / 任务栏图标同一份资源，所以永远一致），边长 21 DIP（`AppLayout.TitleBarLogoSize`；图标里墨迹占边长 72%，故「狐」墨迹高约 15 DIP），左边距按 56 DIP 侧边栏列居中（(56 − 21) / 2 = 17.5 DIP，赋值在 `ApplyLayoutConstants()` 里，`Margin` 是 `Thickness` 不能 `x:Static` 一个 double），因此标识正好落在侧边栏图标列的正上方。ICO 内含 16–256 全尺寸，WPF 按当前 DPI 挑帧（200% 下取 40 / 48 px 帧），不会放大发糊。标识不设 `WindowChrome.IsHitTestVisibleInChrome`，所以按住它仍然能拖窗口。置顶按钮是图钉图标（矢量 `Path`，描边 1 DIP，与三个系统字形同宽），未开启时线框色与最小化 / 最大化 / 关闭一致（`Brush.Caption.Glyph`，浅色 `#5A5A5A` / 深色 `#A8A8A8`），开启时图钉线框变绿、不加背景色（同时把窗口设为 Topmost）；最小化 / 最大化（还原）/ 关闭。最大化被限制在工作区内，不遮挡任务栏。标题栏与侧边栏共用 `Color.Panel.Background`（浅色 `#DCDCE1`，取自微信窗口实时屏幕取色——把微信钉在桌面前沿后用 `CopyFromScreen` 采样，标题栏 / 侧边栏 / 边框三处众数一致；早先从 `wechat.png` 取的 `#E1E1E6` 比实际屏幕深 5 级 / 深色 `#2A2A2A`），与内容区形成层次。面板压深时贴在面板上的四个色块（侧边栏 hover / 选中、标题栏按钮 hover、“+”圆圈 hover）同量跟随，保持相对 Δ 不变。
- **侧边栏**：宽度锁定 56 DIP（一列图标），不可拖拽；该值与窗口尺寸上下限都定义在 `Views/AppLayout.cs`。默认“桌面”项；“+” 新建 Sheet（选一个预设徽章或自选 .ico/.png + 命名，悬停显示名称）；底部为设置入口；长按某个 Sheet 项可以把 “+” 临时变成回收站，用来移除、编辑或**与另一项交换位置**（见下条）。侧边栏底色与窗口自绘边框同色，视觉上左侧没有边框，所以按钮居中时要把 4 DIP 边框算进可见色带（`SidebarView.xaml` 里两处 `Margin` 的右边距 = 边框厚度），否则按钮会比视觉中心偏右 2 DIP。按钮内的矢量图形（“+”、回收站）另有一条约定：**几何从「半描边」处起手**（描边 1.6 → 从 0.8 起画）。因为 `Shape.MeasureOverride` 的期望尺寸是 `bounds.Right/Bottom + 半描边`，把「几何起点到原点的距离」也算进了尺寸，`ContentPresenter` 按这个虚大的框居中就会把墨迹推向右下——原来从 7,7 起手的 “+” 实测偏右下 +2.75 DIP、回收站 +3.25 DIP（与同列的预设徽章差 3 DIP 以上，肉眼能看出 “+” 没对齐）；起点取 0.8 时期望尺寸 = 19.6 = 墨迹实际范围，实测残余偏移 0.5 DIP（1 物理像素）。
- **预设图标**（`Services/PresetIcons.cs`）：新建 / 编辑侧边栏项的对话框里有一排 12 个预设徽章——文件夹、常用、游戏、电子书、文档、开发、图片、音乐、视频、学习、下载、工具。一致性靠构造保证：统一 **24×24 单位网格**，徽章铺满 0..24、圆角 5.5（≈23%，与 app 图标的 22% 同一语言），白色图形限制在 4..20（占 62%~67%，接近 app 图标墨迹 72% 的观感），描边 **1.7** 圆头圆角（与 “+”、回收站、图钉同一套参数），12 个色相各不同但饱和度 / 明度同档。每个预设是一个冻结的 `DrawingImage`（`GeometryDrawing`：徽章 + 图形），它本身就是 `ImageSource`，所以侧边栏与对话框原有的图标通道（`NavItemViewModel.Icon`、预览 `Image`）不用改就能显示，任意 DPI 都锐利。选中预设时名称输入框为空会自动填上预设名（如“游戏”）。
  - 两个坑：`StreamGeometry.BeginFigure(isFilled: false)` 会让 `GeometryDrawing` 的 **Fill 整块消失**（描边正常），所以折线一律标记为填充，只描边的画法传 `Brush=null` 因此不会多出底色；单个几何各自 `Freeze()` 之后就不能再设 `Transform`（锤子 / 扳手要先旋转再让 `DrawingGroup.Freeze()` 连带冻结）。
- **侧边栏长按手势**（`Views/SidebarDragBehavior.cs`，以附加属性 `Host` / `RecycleTarget` 挂在 Sheet 列表的 `ItemsControl` 上）：按住某个 Sheet 项左键 **700 ms** 进入拖拽态——“+” 那个位置换成回收站图标（同样 1.6 描边圆头、同一条居中约定，所以两者在同一位置互换时不会跳），并出现一个跟手的幽灵图标（`GhostAdorner`：`VisualBrush` 画被按住的按钮、0.7 透明度，实测内容 26×26 DIP 且正好以光标为中心）。四种结果，按优先级判定：
  - **拖到回收站上方释放** → 确认框「移除侧边栏项」（`ConfirmDialog` / `ConfirmDialogViewModel`，与其它对话框同风格，确定按钮用红色 `DangerButtonStyle`），确定即移除该侧边栏项，取消什么都不动。指针落在回收站范围内时图标变危险红（`Brush.Menu.Danger`）。
  - **拖到另一个 Sheet 项的范围内释放** → **交换两者位置**（无确认框，直接换；再拖一次就能换回来）。指针压在哪个项上，那个项就画 1.5 DIP 品牌绿描边 + 淡绿底（`Brush.Sidebar.Button.DropTarget`），语义是“松手就落在这里”。
  - **长按后在自己原来的位置释放** → 弹「编辑侧边栏项」，复用新建 Sheet 那个对话框、预填名称与图标，可改名 / 换图标 / 清除图标（换掉或清除时删旧副本）。
  - **长按后在别处释放** = 取消手势，什么都不做；短按（< 700 ms）仍是普通选择，手势不吞 `Click`。
  - 内置「桌面」与底部「设置」不参与手势：`NavItemViewModel.CanDrag` 只对非桌面的自定义 Sheet 为 `true`，它们既不能当拖拽源、也不能当交换目标，长按或拖到它们上面都没有反应（「设置」按钮本来也不在挂行为的 `ItemsControl` 里，双重保险）。“桌面”即使能换也保存不住——`LoadSheets` 永远把它放在第一位。
  - 实现要点：`ItemsControl` 自己 `CaptureMouse`（指针移出侧边栏也收得到 Move / Up），代价是其它元素的 `IsMouseOver` 不再更新，所以回收站高亮与交换目标都按坐标自己算（`HitTestItem`：沿视觉树枚举 `ItemsControl` 里的 `Button`，跳过源项与 `CanDrag == false` 的项，用 `Over()` 换算到窗口根再判定）。命中结果写进附加属性 `SidebarDragBehavior.IsDropTarget`，`SidebarButtonStyle` 的模板触发器据此画描边——**描边厚度常驻（默认透明）**，触发器只换颜色，否则按钮内容会被推偏 1.5 DIP。幽灵挂在窗口根元素的 `AdornerLayer` 上——直接对 `Window` 调 `GetAdornerLayer` 会得到 `null`（这层在窗口模板的 `AdornerDecorator` 里），得从后代往上找第一个能解析出该层的元素。
- **窗口尺寸**：最小宽度 = 侧边栏宽度 × 5（280 DIP），最小高度 520 DIP。窗口的宽高、位置、最大化状态会在调整时写入 `settings.json`，下次启动自动恢复。
- **窗口边框**：`WindowStyle=None`，外框由 `MainWindow` 根部的 `Border` 自绘，宽度 4 DIP（`AppLayout.WindowBorderThickness`，在 `ApplyLayoutConstants()` 里赋值——`BorderThickness` 是 `Thickness` 类型，XAML 里 `x:Static` 一个 `double` 常量会抛 `ArgumentException`）。颜色 `Brush.Window.Border` 与标题栏 / 侧边栏同色（`Color.Panel.Background`），所以只在贴着内容区那一侧看得出。`Border` 会把子元素排在边框厚度以内，用户栏的行底色与分隔线贴到边框内侧，不会盖住右下角。`ResizeBorderThickness="6,6,2,6"`：只有右侧的拖拽改大小边界收窄到 2 DIP（其余三边 6 DIP），这 2 DIP 完全落在 4 DIP 的边框带内，所以拖右边缘改大小仍然有效，同时给贴边的滚动条让出了客户区。外框四角 7 DIP 圆角（`AppLayout.WindowCornerRadius`；赋给 `RootBorder.CornerRadius` 的是 `AppLayout.WindowBorderCornerRadius` = 7 − 4/2 = 5 DIP，因为 WPF 把边框带画在圆角外侧，实测外弧半径 = `CornerRadius + 边框厚度 / 2`，不减就得到 9 DIP。赋值在 `ApplyWindowCornerRadius()` 里——`CornerRadius` 与 `BorderThickness` 一样是结构体，XAML 里 `x:Static` 一个 double 常量同样不行），窗口因此设 `AllowsTransparency="True"` + `Background="Transparent"`，四角真正透明、桌面透出来（代价：WPF 在这种 layered 窗口里用灰度抗锯齿而非 ClearType，192 DPI 下仍干净）。最大化时内外圆角都归零（`OnStateChanged` 调用同一个方法），与 Windows 11 一致。
- **单实例**：再次启动不会开新窗口，而是把已有窗口唤到前台（最小化时先还原），第二个实例自行退出（退出码 0）。用命名 Mutex 判定，进程崩溃或被强杀时由系统释放，不会留下“再也打不开”的死锁；拦截发生在建窗口与读配置之前，所以被拒的实例不会写任何文件。管理员权限与普通权限各算一个实例（Windows 完整性级别限制）。
- **搜索栏**：过滤当前 Sheet 的行，只显示命中的行。右侧圆形 “+” 添加一行（选择文件 / 文件夹 / 快捷方式，名称默认取目标本来的名称），新行排在所有置顶行之后、打开次数相同（都是 0 次）的非置顶行最前面。内置“桌面”Sheet 为只读扫描，该按钮禁用。
- **用户栏**：每行显示目标原始图标 + 名称。单击打开（文件夹 / 应用 / 文档）。右键菜单：置顶 / 取消置顶、修改（改图标与名称）、打开、从列表移除——最后这项**只有自定义 Sheet 有**：桌面 Sheet 的行来自磁盘扫描、没有可删的记录，要去掉它只能删磁盘文件，而本工具从不删磁盘文件，所以菜单里直接不显示（绑 `LinkItemViewModel.CanRemove`）。菜单为自定义模板：菜单项通栏（不用 WPF 默认模板那条 `#F1F1F1` 图标列），hover 时微信绿底（`Brush.Accent`）白字；“从列表移除”用危险色 `Brush.Menu.Danger`（`#FA5152`），hover 时红底白字。

- **置顶色带**：置顶行底色比正常行深一档，与微信列表一致（浅色 `Brush.Row.Pinned` `#E6E6E8` / `Brush.Row.Normal` `#EEEEF0`；深色 `#262626` / `#2E2E2E`）。置顶行排序在最前，所以底色自动连成一条色带。搜索栏那条色带跟随用户栏第一行（`SheetViewModel.IsTopRowPinned`），置顶时深色带从内容区顶部开始，不会在搜索栏下方被截断；内容区底色与正常行同值，最后一行以下没有接缝。行分隔线用 `Brush.Row.Separator`（微信实测是比色带更深的灰线，浅色 `#DDDDDF` / 深色 `#1F1F1F`）。

- **内容区圆角**：搜索栏 + 用户栏是一个整体，四角统一 6 DIP 倒角（`AppLayout.ContentCornerRadius`），由 `Views/RoundClipBehavior.cs` 的附加属性 `RoundClip.Radius` 在 `SizeChanged` 时写 `UIElement.Clip = new RectangleGeometry(new Rect(0,0,ActualWidth,ActualHeight), r, r)`。不能用 `Border.CornerRadius`：它只把自己的底色画成圆角，行底色、分隔线、滚动条这些子元素仍然是直角。裁剪跟随窗口尺寸（拉伸、最小尺寸、最大化都重算），四角缺口露出面板底色，四边中点仍然贴到自绘边框内侧；`Clip` 同时影响命中测试，四个角的小缺口不再响应点击。设置页 `SettingsView` 用同一个常量。窗口内层 `RootGrid` 挂的是同一个行为，半径同为 6 DIP（`AppLayout.WindowInnerCornerRadius`）——内容块的右下角就是窗口的内右下角，两条弧重合才不会在同一个角上出现两种半径。外框取 7 DIP（比同心的 6 + 4 = 10 DIP 小），所以转角处的边框带约 5.2 DIP（直边处仍是 4 DIP）。

- **行状态优先级**：底色 trigger 全部写在 `RowItemStyle` 的 `Style.Triggers`（样式 trigger 优先于模板 trigger，同一集合内**后声明的赢**），顺序为 置顶 → hover → 焦点。**选中态刻意不上色**：这一列的点击语义是「打开」而不是「选中」——全项目没有 `SelectedItem` / `SelectedIndex` / `SelectionChanged` 的消费方，右键菜单走 `PlacementTarget.DataContext`，启动时也没有默认选中，所以 `Brush.Row.Selected`（浅色 `#D3D3D3` / 深色 `#454545`）已从两个主题里删掉；否则「点一行 → 焦点移到搜索框」之后，那行会留下一块比基础灰深一档、又没有含义的色块。代价：焦点不在列表上时，列表里没有「刚才点的是哪一行」的标记。焦点行（`IsKeyboardFocused`）用 `Brush.Row.Focused`（微信选中行实测 `#15AC70`，与品牌绿 `Brush.Accent` `#07C160` 不是一个值）+ `Brush.Text.OnAccent` 白字，同时把 `FocusVisualStyle` 设为 `{x:Null}` 去掉虚线焦点框；窗口失活时 WPF 丢掉键盘焦点，绿底自动撤掉，回到前台又把焦点还原到原来那行、绿底自动回来。**hover 额外要求 `Window.IsActive=True`**（`MultiDataTrigger`，条件里用 `RelativeSource Self` 取容器自身的属性——`Condition` 的默认绑定源是 `DataContext`，写 `{Binding IsMouseOver}` 会去问 VM 而不是 `ListBoxItem`）：窗口未激活时鼠标仍可能停在行上，不加这个条件就会露出 hover 灰；条件不成立时底色自动回落到基础色（置顶 `#E6E6E8` / 普通 `#EEEEF0`），不必在 trigger 里重复写基础色。

- **滚动条**：用户栏用自定义 `ScrollBar` 模板（定义在 `RowListBoxStyle` 的 `Style.Resources`，只作用于这个列表）。宽度 8 DIP（系统默认 17 DIP 的一半左右），必须同时把 `MinWidth/MinHeight` 清零，否则主题样式里的系统宽度下限会把 `Width=8` 顶回去。模板只有 `Track` + `Thumb`，上下箭头按钮不进模板；轨道两端的翻页 `RepeatButton` 保留但模板全透明，所以点轨道仍能翻页，视觉上只剩滑块。列表的 `ScrollViewer` 也换了模板（内容 presenter 与滚动条同处一格），滚动条**浮在行之上**而不是独占一列，置顶色带和分隔线因此能一直铺到窗口右边缘；滚动条自身只留 2 DIP 右边距（贴着自绘边框内侧，实测滑块右缘离边框 4 物理 px）；能这么做是因为窗口右侧的 resize 边界已收窄到 2 DIP（见上条），否则 8 DIP 的滑块会整个落进非客户区，既悬停不到也拖不动。滑块 `Brush.Scroll.Thumb`（浅色 `#CDCDCD` / 深色 `#5A5A5A`），按住或悬停时变 `Brush.Scroll.Thumb.Hover`（`#B8B8B8` / `#6E6E6E`）。空闲时淡出：`Views/ScrollRevealBehavior.cs` 监听列表的 `MouseEnter/MouseLeave` 与冒泡上来的 `ScrollChanged`，写入只读附加属性 `IsRevealed`（停止滚动后保持 400ms），滚动条样式用 `DataTrigger` 的 `EnterActions/ExitActions` 淡入淡出（0.15s / 0.35s），默认 `Opacity=0`。

## 应用图标

`src/DesktopManager/Assets/app.ico`：圆角正方形（半径 = 边长 22%）+ 纯色微信绿 `#07C160`（与 `Brush.Accent` 同值）+ 白色行书「狐」（`STXingkai` / 华文行楷）。生成脚本 `tools/icon-gen/MakeIcon.ps1`，可重跑：

```powershell
powershell -STA -File tools\icon-gen\MakeIcon.ps1 -Out src\DesktopManager\Assets\app.ico -Dump <导出目录>
```

- 字形取 `FormattedText.BuildGeometry` 的**矢量轮廓**（不是把字当文本画），按墨迹包围盒做光学居中。华文行楷的「狐」墨迹宽 / 高 = 1.258，所以占比按最长边算（72%，256 画布上 184×146）；按字高算 64% 的话字宽会到 80%，顶到左右边。
- 条目 16 / 20 / 24 / 32 / 40 / 48 / 64 / 96 / 128 / 256：≤48 用无压缩 32bpp BGRA DIB + AND mask（兼容性最好），≥64 用 PNG 条目；20 / 40 是给 125% / 200% DPI 的。
- ≤24 px 单独把字形占比提到 82% 并加 0.4 px 描边（行楷笔画细，16 px 会断笔）。
- 每个尺寸**直接按目标尺寸栅格化矢量**（`-Sup 1`）。4× 超采样再用 `BitmapScalingMode.HighQuality` 降采样会在透明圆角处振铃：16 px 的角像素 alpha 从 0 变成 254，圆角被填平，笔画周围还有噪点。
- 接线三处：`<ApplicationIcon>`（exe 图标）、`<Resource Include="Assets\app.ico" />`、`MainWindow.xaml` 的 `Icon=`（任务栏 / Alt+Tab 明确用它，不依赖 exe 回退）。窗口标题栏本来就不显示图标，所以界面上没有变化。
- 脚本自带校验：读回 ICO 目录（条目数 / 尺寸 / 偏移，DIB 头必须正好 40 字节、条目大小 = XOR + AND mask 之和），再用 WPF `IconBitmapDecoder` 与 GDI+ `Icon` 各解一遍，并把每个条目导出成 PNG 供肉眼检查（顺带打印角点 / 中心 alpha，确认圆角真的透明）。
- 脚本里两个 PowerShell 5.1 陷阱：`$bw.Write($byteArray)` 会绑到 `BinaryWriter.Write(byte)` 只写 1 个字节（要用 `Stream.Write(buf, 0, n)`）；`BitmapSource.CopyPixels` 在这里报的缓冲区大小是错的（连 16×16 的纯色目标都要 15424 字节），取像素走 GDI+ `LockBits`。脚本文件本身必须保持纯 ASCII——PS 5.1 按 ANSI 读 `.ps1`，UTF-8 中文注释的字节会被解成 GBK 的 `}` / `"`，直接语法错误。
- 换图标后任务栏 / 资源管理器可能还显示旧图标（shell 按 exe 路径缓存图标），`ie4uinit.exe -show` 或重启 explorer 才刷新；`ExtractAssociatedIcon` 与窗口自身的 `WM_GETICON` 不受缓存影响，可用来确认 exe 与窗口图标确实已经换掉。

## 代码结构（MVVM）

```
src/DesktopManager/
├─ Models/          纯数据：SheetDefinition、LinkItem、AppSettings（无 WPF 依赖）
├─ Services/        接口 + 实现，构造注入
│   ├─ IStore / JsonStore        %APPDATA%\DesktopManager\{sheets,settings}.json + icons\
│   ├─ PresetIcons               12 个预设徽章：矢量 DrawingImage + key 查表（不落盘）
│   ├─ IShellService             SHGetFileInfo 取图标、IShellLinkW 解析 .lnk、ShellExecute 打开
│   ├─ IIconCache                按 path + 修改时间缓存图标
│   ├─ IDesktopScanner           合并 当前用户桌面 + 公共桌面
│   ├─ IDialogService            文件/文件夹选择与各对话框（带门闩：同一类对话框不会叠两层，对话框内仍可选文件）
│   ├─ IThemeService             整体替换主题字典
│   ├─ IMonitorService           EnumDisplayMonitors 取各显示器工作区（恢复窗口位置时校验）
│   ├─ ISingleInstanceGuard      命名 Mutex 单实例闸门（进程崩溃 / 被强杀时由系统释放）
│   ├─ InstanceActivator         把已在运行的实例的主窗口还原并唤到前台
│   └─ IWindowController         VM 控制窗口（最小化/最大化/关闭、下发上次窗口边界）而不引用 Window
├─ ViewModels/      MainViewModel、SidebarViewModel、NavItemViewModel、
│                   SheetViewModel、LinkItemViewModel、SettingsViewModel、
│                   NewSheetDialogViewModel、LinkEditDialogViewModel、ConfirmDialogViewModel
│                   （CommunityToolkit.Mvvm：[ObservableProperty] / [RelayCommand]）
├─ Views/           MainWindow、SidebarView、SheetView、SettingsView、
│                   三个对话框（新建 / 编辑 Sheet、编辑行、确认框）
│                   AppLayout（侧边栏宽度 / 窗口上下限 / 默认尺寸）、
│                   MouseClickBehavior（单击行即打开）、RoundClipBehavior（内容区圆角裁剪）、
│                   ScrollRevealBehavior（滚动条自动淡出）、SidebarDragBehavior（侧边栏长按拖拽：
│                   回收站移除 / 原地编辑 / 拖到另一个项上交换）、
│                   Controls.xaml 里的 PresetStripStyle / PresetTileStyle（预设磁贴单选条）
└─ Themes/          Light.xaml / Dark.xaml（同名颜色键）+ Controls.xaml（样式）
```

解耦要点：

- ViewModel 不出现 `MessageBox`、`OpenFileDialog`、`Process.Start`、P/Invoke，全部通过服务接口。
- View 与 ViewModel 通过 `App.xaml` 中的隐式 `DataTemplate` 关联，内容区只绑定 ViewModel。
- 所有颜色用 `DynamicResource`，切换主题字典即全局换色。

## 数据

- 配置与数据：`%APPDATA%\DesktopManager\sheets.json`、`settings.json`
- 自选图标会复制进 `%APPDATA%\DesktopManager\icons\`，原文件删除不影响显示。复制发生在点“确定”时（取消不留副本）；换图标或移除条目会删掉不再被引用的旧副本
- 预设图标只往 `sheets.json` 写一个 key（`PresetIcon`，`JsonIgnoreCondition.WhenWritingNull`，没选就不落盘，老数据不用迁移），**不产生任何文件**；它与 `IconPath` 互斥——选预设时对话框会把 `IconPath` 清空，调用方发现旧路径与新值不同就删掉旧副本；两者都为空则回落到内置字形（自定义 Sheet 用 `E8B7`）
- 移除侧边栏项（长按拖到回收站）删的是记录本体：`SheetDefinition` + 它的全部 `LinkItem`（各自的置顶时间、打开次数一并消失）+ Sheet 自己与每一行的图标副本，磁盘上的目标文件一律不碰。被移除的正是当前项时回落到第一个 Sheet（通常是“桌面”）并更新 `LastSheetId`
- 侧边栏顺序存在 `SheetDefinition.Order` 里，`sheets.json` 里的先后 = 界面上的先后。交换两项位置（长按拖到另一个项上释放）会把 `Sheets` 与 `Sidebar.Items` 两个集合按索引对调，然后把自定义 Sheet 的 `Order` **重编为 1..n**（内置“桌面”保持 `int.MinValue`，永远排第一）——不重编的话，`AddSheet` 用 `Sheets.Count` 当 `Order`，删过 Sheet 之后会出现重复值，只交换两个值在重复值上等于没交换。交换两次换回来，文件字节级一致
- 图标缓存是 LRU，上限 2048 条；用户自选的位图按 160px 解码（`.ico` 除外），大图不会整张常驻内存
- 桌面 Sheet 每次切换会重扫目录，但条目模型与行对象按路径缓存复用；桌面内容没变时完全不动列表（实测 Reload 成本 ~90ms → ~4ms）
- 只有设置变化（主题 / 置顶 / 窗口几何 / 上次 Sheet）时只写 `settings.json`，不重写 `sheets.json`；数值没变则完全不写盘
- 排序（`SheetViewModel` 的 `SortDescriptions`，五条链）：置顶行在前 → **置顶时间降序**（后置顶的排最前）→ **打开次数降序**（打开越多次越靠前）→ `SortOrder` 升序 → 名称升序。置顶项 `PinnedAt` 全非 null、非置顶全为 null，降序下 null 彼此相等，所以非置顶区直接落到“打开次数”这一条——**置顶项不参与次数排名**。新行的 `SortOrder` 取当前最小值减一，在次数相同（都是 0）的非置顶行里排最前
- 打开次数：每次**成功**打开 +1（`_shell.Open` 返回 false 即目标丢失 / 没有关联程序时不计数、不写盘），存在记录的 `OpenCount` 字段里，标了 `JsonIgnoreCondition.WhenWritingDefault`，0 不落盘。桌面条目在 `sheets.json` 里本来可能没有记录，第一次打开会新建一条只含路径与次数的覆盖项。打开**当场故意不重排**（免得点一下就跳位），切回该 Sheet 时由 `MainViewModel.SelectContent` → `SheetViewModel.RefreshOrder` 生效
- 内置“桌面”Sheet 的置顶 / 改名 / 自定义图标 / 打开次数 作为“覆盖项”按路径保存，不会改动磁盘上的桌面文件。桌面 Sheet 没有“从列表移除”，也没有隐藏机制（旧的 `Hidden` 字段与相关代码已全部删除，`sheets.json` 里残留的 `Hidden` 键会被读取时忽略、下次写盘自动消失）
- 覆盖项回收：桌面文件被删除或在资源管理器里改名后，它的覆盖项再也用不到，`PruneMissing` 会在下一次扫描桌面时把它从 `sheets.json` 删掉（连同置顶、改名、自定义图标、打开次数）。构造期间不回收也不写盘——那时本 Sheet 还没进 `MainViewModel.Sheets`，写一次就把它整个漏掉
- 窗口状态（`WindowWidth/Height/Left/Top`、`WindowMaximized`）也存 `settings.json`：拖动 / 改变大小后 500ms 防抖写入，关闭前再补写一次；最大化时只更新标志，不会把全屏尺寸污染成正常尺寸。恢复时先抬到上下限、再压到所在显示器工作区，位置落在已断开的显示器上则保留尺寸并居中

## 已知限制

- 桌面 Sheet 只读：不显示隐藏 / 系统文件，也不写入桌面
- 桌面 Sheet 的行不能从列表里去掉（没有“从列表移除”），只能在资源管理器里删除或改名；改名后旧覆盖项会被回收，新名字从 0 次开始
- 自定义 Sheet 的改名与删除只有“长按手势”一个入口（行级右键菜单里没有，Sheet 项也没有右键菜单）；内置“桌面”与“设置”两项不能改名也不能移除
- 移除侧边栏项不可撤销：确认框点“确定”后该 Sheet 的全部记录与图标副本立即删除，没有回收站还原，也没有二次确认
- 长按阈值 700 ms 是写死的常量（`SidebarDragBehavior.HoldMs`），不能在设置里调；拖拽过程中按 Esc 也不取消，只能在回收站之外释放
- 侧边栏排序只有“交换”一种手势：拖到另一个项上释放是**两项互换**，不能插入到两个项之间（那需要插入指示线，语义也不同）。内置“桌面”与“设置”既不能被拖也不能被换——“桌面”启动时永远排第一，换了也保存不住
- 侧边栏的 Sheet 列表没有滚动条：Sheet 多到超出窗口高度时会溢出看不全（交换与移除都只能操作看得见的项）
- 自定义 Sheet 的记录不会自动清理：目标被删掉或移走后该行仍然显示，点它会弹“打开失败”（不计入打开次数），需要自己“从列表移除”
- “从列表移除”只删记录（以及它独占的图标副本），**从不删磁盘上的文件或文件夹**
- 预设图标固定 12 款、写死在 `PresetIcons.cs`，不能在界面上新增；徽章是彩色的，**不跟随主题色**（与自选文件图标行为一致，深浅主题下都清楚），只有内置字形会跟着 `Brush.Sidebar.Glyph` 换色
- 预设图标只作用于侧边栏项；行图标（每行文件）仍然只能用目标原始图标或自选 .ico/.png
- 图标提取失败时该行显示空白图标（不会崩溃）
