using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class IconGenerator
{
    private static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };

    private static void Main(string[] args)
    {
        if (args.Length != 1)
            throw new ArgumentException("An output .ico path is required.");

        List<byte[]> images = new List<byte[]>();
        foreach (int size in Sizes)
            images.Add(RenderPng(size));

        using (FileStream stream = File.Create(args[0]))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)Sizes.Length);

            int offset = 6 + (16 * Sizes.Length);
            for (int i = 0; i < Sizes.Length; i++)
            {
                int size = Sizes[i];
                writer.Write((byte)(size == 256 ? 0 : size));
                writer.Write((byte)(size == 256 ? 0 : size));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write(images[i].Length);
                writer.Write(offset);
                offset += images[i].Length;
            }

            foreach (byte[] image in images)
                writer.Write(image);
        }
    }

    private static byte[] RenderPng(int size)
    {
        using (Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);

            float inset = Math.Max(1F, size * 0.055F);
            RectangleF bounds = new RectangleF(inset, inset, size - (inset * 2F), size - (inset * 2F));
            float radius = size * 0.21F;

            using (GraphicsPath shadowPath = RoundedRectangle(
                new RectangleF(bounds.X, bounds.Y + Math.Max(1F, size * 0.025F), bounds.Width, bounds.Height), radius))
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(130, 0, 6, 35)))
                graphics.FillPath(shadow, shadowPath);

            using (GraphicsPath path = RoundedRectangle(bounds, radius))
            using (LinearGradientBrush background = new LinearGradientBrush(
                bounds, Color.FromArgb(84, 145, 255), Color.FromArgb(33, 67, 210), 135F))
            {
                graphics.FillPath(background, path);
                using (Pen border = new Pen(Color.FromArgb(205, 145, 210, 255), Math.Max(1F, size * 0.018F)))
                    graphics.DrawPath(border, path);
            }

            float fontSize = size * 0.58F;
            using (Font font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
            using (StringFormat format = new StringFormat())
            using (SolidBrush letterShadow = new SolidBrush(Color.FromArgb(100, 0, 8, 55)))
            using (SolidBrush letter = new SolidBrush(Color.White))
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                RectangleF textBounds = new RectangleF(0, -size * 0.035F, size, size);
                RectangleF shadowBounds = textBounds;
                shadowBounds.Y += Math.Max(1F, size * 0.02F);
                graphics.DrawString("K", font, letterShadow, shadowBounds, format);
                graphics.DrawString("K", font, letter, textBounds, format);
            }

            using (MemoryStream stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
        }
    }

    private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        float diameter = radius * 2F;
        GraphicsPath path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
