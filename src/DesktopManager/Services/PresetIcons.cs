using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace DesktopManager.Services;

/// <summary>
/// 侧边栏预设图标：彩色圆角徽章 + 白色矢量图形。
/// 用 <see cref="DrawingImage"/> 表示，本身就是 <c>ImageSource</c>，所以侧边栏与对话框
/// 现有的图标通道（<c>NavItemViewModel.Icon</c> / 预览 <c>Image</c>）不用改就能显示，
/// 而且任意 DPI 都锐利、不产生任何文件。
/// </summary>
/// <param name="Key">写进 <c>SheetDefinition.PresetIcon</c> 的稳定标识</param>
/// <param name="Label">磁贴 ToolTip，也是名称为空时的自动填充值</param>
/// <param name="Image">渲染用的矢量图</param>
public sealed record PresetIcon(string Key, string Label, DrawingImage Image);

/// <summary>
/// 预设图标表。风格与尺寸靠构造保证一致：
/// <list type="bullet">
/// <item>统一 24×24 单位网格，徽章铺满 0..24，圆角 5.5（≈23%，与 app 图标的 22% 同一语言）</item>
/// <item>白色图形限制在 4..20（占徽章 62%~67%，接近 app 图标墨迹 72% 的观感）</item>
/// <item>描边 1.7、圆头圆角 —— 与 “+”、回收站、图钉同一套参数</item>
/// <item>徽章色各自一个色相，饱和度/明度大致同档，整套读起来是一个家族；彩色且不跟随主题，
/// 与“自选文件图标”的行为一致（深色主题下同样清楚）</item>
/// </list>
/// </summary>
public static class PresetIcons
{
    /// <summary>矢量网格边长（单位，不是 DIP）。</summary>
    public const double Grid = 24;

    private const double BadgeRadius = 5.5;
    private const double InkStroke = 1.7;

    private static readonly Brush Ink = Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)));

    private static readonly Pen InkPen = Frozen(new Pen(Ink, InkStroke)
    {
        StartLineCap = PenLineCap.Round,
        EndLineCap = PenLineCap.Round,
        LineJoin = PenLineJoin.Round,
    });

    /// <summary>对话框里按此顺序展示。</summary>
    public static readonly IReadOnlyList<PresetIcon> All = new List<PresetIcon>
    {
        new("folder", "文件夹", Badge(0x3E, 0x8B, 0xFF, g =>
        {
            // 带左凸标签的文件夹轮廓
            Stroke(g, Poly(true, 4.6, 17.8, 4.6, 8.4, 9.2, 8.4, 10.9, 6.6, 19.4, 6.6, 19.4, 17.8));
        })),
        new("star", "常用", Badge(0xF5, 0xA6, 0x23, g => Fill(g, Star(12, 12.4, 7.4, 3.5)))),
        new("game", "游戏", Badge(0x07, 0xC1, 0x60, g =>
        {
            Stroke(g, RR(4.2, 8.6, 15.6, 8.2, 4.1));
            Stroke(g, Line(7.0, 12.7, 9.6, 12.7));
            Stroke(g, Line(8.3, 11.4, 8.3, 14.0));
            Fill(g, Circle(14.9, 11.7, 1.15));
            Fill(g, Circle(16.9, 13.7, 1.15));
        })),
        new("book", "电子书", Badge(0xE8, 0x68, 0x4A, g =>
        {
            // 摊开的两页 + 书脊
            Stroke(g, Poly(true, 12.0, 7.9, 5.2, 6.5, 5.2, 16.6, 12.0, 18.0));
            Stroke(g, Poly(true, 12.0, 7.9, 18.8, 6.5, 18.8, 16.6, 12.0, 18.0));
        })),
        new("doc", "文档", Badge(0x5A, 0x6A, 0xCF, g =>
        {
            Stroke(g, Poly(true, 6.4, 5.2, 14.6, 5.2, 17.6, 8.2, 17.6, 18.8, 6.4, 18.8));
            Stroke(g, Poly(false, 14.6, 5.2, 14.6, 8.2, 17.6, 8.2));
            Stroke(g, Line(8.9, 12.2, 15.1, 12.2));
            Stroke(g, Line(8.9, 15.0, 13.4, 15.0));
        })),
        new("code", "开发", Badge(0x7A, 0x5A, 0xF8, g =>
        {
            Stroke(g, Poly(false, 9.2, 8.4, 5.6, 12.0, 9.2, 15.6));
            Stroke(g, Poly(false, 14.8, 8.4, 18.4, 12.0, 14.8, 15.6));
            Stroke(g, Line(13.1, 7.2, 10.9, 16.8));
        })),
        new("image", "图片", Badge(0x12, 0xA5, 0x94, g =>
        {
            Stroke(g, RR(4.8, 5.8, 14.4, 12.4, 2.2));
            Fill(g, Circle(9.0, 9.7, 1.4));
            Stroke(g, Poly(false, 6.2, 16.2, 10.1, 11.9, 12.9, 14.6, 15.1, 12.5, 17.8, 16.2));
        })),
        new("music", "音乐", Badge(0xD9, 0x45, 0x5F, g =>
        {
            Stroke(g, Line(9.6, 16.4, 9.6, 7.6));
            Stroke(g, Line(17.0, 15.0, 17.0, 6.2));
            Stroke(g, Line(9.6, 7.6, 17.0, 6.2));
            Fill(g, Circle(7.4, 16.4, 2.2));
            Fill(g, Circle(14.8, 15.0, 2.2));
        })),
        new("video", "视频", Badge(0xE0, 0x71, 0x2A, g =>
        {
            Stroke(g, RR(4.6, 6.2, 14.8, 11.6, 2.4));
            Fill(g, Poly(true, 10.2, 9.1, 15.2, 12.0, 10.2, 14.9));
        })),
        new("study", "学习", Badge(0x2F, 0x8F, 0xA0, g =>
        {
            // 学士帽：菱形帽板 + 帽身 + 流苏
            Fill(g, Poly(true, 12.0, 6.2, 20.0, 10.0, 12.0, 13.8, 4.0, 10.0));
            Stroke(g, Poly(false, 8.4, 12.2, 8.4, 15.8, 15.6, 15.8, 15.6, 12.2));
            Stroke(g, Line(18.4, 11.0, 18.4, 15.2));
            Fill(g, Circle(18.4, 16.4, 1.0));
        })),
        new("download", "下载", Badge(0x4C, 0x6F, 0xFF, g =>
        {
            Stroke(g, Line(12.0, 5.2, 12.0, 14.6));
            Stroke(g, Poly(false, 8.2, 10.9, 12.0, 14.8, 15.8, 10.9));
            Stroke(g, Poly(false, 5.2, 15.4, 5.2, 18.6, 18.8, 18.6, 18.8, 15.4));
        })),
        new("tools", "工具", Badge(0x6B, 0x72, 0x80, g =>
        {
            // 锤子：先正着画（横向锤头 + 竖直柄），再整体转 40°，斜向比正着更像工具
            Fill(g, Rotated(RR(7.6, 5.6, 8.8, 3.2, 1.2), 40));
            Stroke(g, Rotated(Line(12, 8.8, 12, 18.4), 40));
        })),
    };

    /// <summary>按 key 取预设；未知或为空的 key 返回 null（回落到内置字形）。</summary>
    public static PresetIcon? Find(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        foreach (var preset in All)
        {
            if (string.Equals(preset.Key, key, StringComparison.Ordinal))
            {
                return preset;
            }
        }

        return null;
    }

    private static DrawingImage Badge(byte r, byte gIn, byte b, Action<DrawingGroup> draw)
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(
            Frozen(new SolidColorBrush(Color.FromRgb(r, gIn, b))),
            null,
            new RectangleGeometry(new Rect(0, 0, Grid, Grid), BadgeRadius, BadgeRadius)));

        draw(group);

        // 整组冻结一次就够了：Freeze 会连带冻结子图与其中的几何，
        // 所以单个几何不必各自冻结（各自冻结反而会让后面设 Transform 抛异常）
        group.Freeze();

        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }

    private static void Stroke(DrawingGroup group, Geometry geometry)
        => group.Children.Add(new GeometryDrawing(null, InkPen, geometry));

    private static void Fill(DrawingGroup group, Geometry geometry)
        => group.Children.Add(new GeometryDrawing(Ink, null, geometry));

    private static RectangleGeometry RR(double x, double y, double w, double h, double radius)
        => new(new Rect(x, y, w, h), radius, radius);

    private static EllipseGeometry Circle(double cx, double cy, double radius)
        => new(new Point(cx, cy), radius, radius);

    private static LineGeometry Line(double x1, double y1, double x2, double y2)
        => new(new Point(x1, y1), new Point(x2, y2));

    /// <summary>
    /// 折线（坐标按 x,y 成对给出）。closed 时首尾相连，闭合边同样会被描边。
    /// isFilled 必须给 true：GeometryDrawing 的 Fill 只认标记为填充的图形，
    /// 给 false 时描边正常、填充整块消失（星形 / 播放三角 / 音符头都踩过这个坑）。
    /// 只描边的画法传 Brush=null，所以标记成填充也不会多出底色。
    /// </summary>
    private static StreamGeometry Poly(bool closed, params double[] points)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(points[0], points[1]), isFilled: true, isClosed: closed);
            for (var i = 2; i < points.Length; i += 2)
            {
                context.LineTo(new Point(points[i], points[i + 1]), isStroked: true, isSmoothJoin: true);
            }
        }

        return geometry;
    }

    /// <summary>五角星，10 个顶点（外径 / 内径交替），第一个顶点朝上。</summary>
    private static StreamGeometry Star(double cx, double cy, double outer, double inner)
    {
        var points = new double[20];
        for (var i = 0; i < 10; i++)
        {
            var angle = (i * 36 - 90) * (Math.PI / 180.0);
            var radius = i % 2 == 0 ? outer : inner;
            points[i * 2] = cx + radius * Math.Cos(angle);
            points[i * 2 + 1] = cy + radius * Math.Sin(angle);
        }

        return Poly(true, points);
    }

    /// <summary>绕网格中心旋转（正角 = 屏幕上的顺时针）。必须在冻结之前调用。</summary>
    private static Geometry Rotated(Geometry geometry, double angle)
    {
        geometry.Transform = Frozen(new RotateTransform(angle, Grid / 2, Grid / 2));
        return geometry;
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
