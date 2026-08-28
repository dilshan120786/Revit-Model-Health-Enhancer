using System.Windows;
using System.Windows.Media;

namespace RevitHealthEnhancer.UI
{
    internal static class RibbonIcons
    {
        public static ImageSource CreateHealthIcon()
        {
            DrawingGroup group = new DrawingGroup();

            using (DrawingContext context = group.Open())
            {
                Rect bounds = new Rect(0, 0, 32, 32);
                Brush background = new SolidColorBrush(Color.FromRgb(30, 96, 145));
                Brush accent = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                Pen whitePen = new Pen(Brushes.White, 2.2);
                Pen accentPen = new Pen(accent, 2.4);

                context.DrawRoundedRectangle(background, null, bounds, 5, 5);
                context.DrawEllipse(accent, null, new Point(24, 8), 4, 4);

                Geometry pulse = Geometry.Parse("M 5 18 L 10 18 L 13 10 L 18 25 L 21 18 L 27 18");
                context.DrawGeometry(null, whitePen, pulse);

                context.DrawLine(accentPen, new Point(6, 26), new Point(26, 26));
            }

            group.Freeze();

            DrawingImage image = new DrawingImage(group);
            image.Freeze();
            return image;
        }

        public static ImageSource CreateInfoIcon()
        {
            DrawingGroup group = new DrawingGroup();

            using (DrawingContext context = group.Open())
            {
                Rect bounds = new Rect(0, 0, 32, 32);
                Brush background = new SolidColorBrush(Color.FromRgb(78, 93, 108));
                Brush circle = new SolidColorBrush(Color.FromRgb(236, 240, 241));
                Brush text = new SolidColorBrush(Color.FromRgb(44, 62, 80));
                Pen textPen = new Pen(text, 3);

                context.DrawRoundedRectangle(background, null, bounds, 5, 5);
                context.DrawEllipse(circle, null, new Point(16, 16), 10, 10);
                context.DrawEllipse(text, null, new Point(16, 10), 1.8, 1.8);
                context.DrawLine(textPen, new Point(16, 15), new Point(16, 22));
            }

            group.Freeze();

            DrawingImage image = new DrawingImage(group);
            image.Freeze();
            return image;
        }
    }
}
