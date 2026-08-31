// © Mayanktaker Computers & Web Development | https://mayanktaker.com

using System;
using System.Collections.Generic;
using Cairo;
using Gtk;
using XDM.Core.Util;

namespace XDM.GtkUI.Controls
{
    // Real-time dynamic throughput waveform graph with smooth Bézier gradient fill and peak metrics
    public class SpeedGraphWidget : DrawingArea
    {
        private const int SampleCount = 60;
        private readonly double[] samples = new double[SampleCount];
        private int currentIndex = 0;
        private double maxSpeed = 100 * 1024; // 100 KB/s baseline
        private double currentSpeed = 0;
        private double peakSpeed = 0;
        private bool isDarkMode = true;

        public SpeedGraphWidget()
        {
            HeightRequest = 76;
            Hexpand = true;
            Valign = Align.Fill;
            Halign = Align.Fill;
            MarginStart = 10;
            MarginEnd = 10;
            MarginTop = 4;
            MarginBottom = 4;
            StyleContext.AddClass("speed-graph-widget");
        }

        // Pushes a new speed data point in bytes/second and requests a redraw
        public void AddSample(long bytesPerSec)
        {
            currentSpeed = bytesPerSec;
            if (currentSpeed > peakSpeed)
            {
                peakSpeed = currentSpeed;
            }

            samples[currentIndex] = currentSpeed;
            currentIndex = (currentIndex + 1) % SampleCount;

            // Recalculate dynamic max speed scale with smoothing headroom
            double highest = 100 * 1024;
            for (int i = 0; i < SampleCount; i++)
            {
                if (samples[i] > highest)
                {
                    highest = samples[i];
                }
            }
            maxSpeed = highest * 1.15; // 15% headroom

            QueueDraw();
        }

        // Resets current throughput to zero (e.g. when downloads pause or finish)
        public void ResetSpeed()
        {
            currentSpeed = 0;
            AddSample(0);
        }

        // Renders the Cairo waveform, background card, grid lines, and speed badges
        protected override bool OnDrawn(Context cr)
        {
            int width = AllocatedWidth;
            int height = AllocatedHeight;
            if (width <= 0 || height <= 0)
            {
                return true;
            }

            isDarkMode = StyleContext.HasClass("dark") || !StyleContext.HasClass("light");

            double paddingX = 14;
            double paddingY = 12;
            double plotWidth = width - (paddingX * 2);
            double plotHeight = height - (paddingY * 2);
            double bottomY = height - paddingY;

            // 1. Draw rounded background card
            DrawCardBackground(cr, width, height);

            // 2. Draw subtle dashed horizontal grid guidelines
            DrawGridLines(cr, paddingX, paddingY, plotWidth, plotHeight);

            // 3. Render throughput waveform with gradient underfill
            DrawWaveform(cr, paddingX, paddingY, plotWidth, plotHeight, bottomY);

            // 4. Render top metrics badge (current throughput + peak speed)
            DrawMetricsBadges(cr, width, paddingX, paddingY);

            return true;
        }

        // Draws the rounded container surface with subtle glassmorphic border
        private void DrawCardBackground(Context cr, int width, int height)
        {
            double radius = 12;
            DrawRoundedRectangle(cr, 0.5, 0.5, width - 1, height - 1, radius);

            if (isDarkMode)
            {
                cr.SetSourceRGBA(0.11, 0.11, 0.11, 0.95);
                cr.FillPreserve();
                cr.SetSourceRGBA(1.0, 1.0, 1.0, 0.08);
                cr.LineWidth = 1.0;
                cr.Stroke();
            }
            else
            {
                cr.SetSourceRGBA(0.96, 0.96, 0.96, 0.95);
                cr.FillPreserve();
                cr.SetSourceRGBA(0.0, 0.0, 0.0, 0.09);
                cr.LineWidth = 1.0;
                cr.Stroke();
            }
        }

        // Draws subtle guide lines at 50% and 100% throughput scale
        private void DrawGridLines(Context cr, double paddingX, double paddingY, double plotWidth, double plotHeight)
        {
            cr.Save();
            cr.SetDash(new double[] { 3.0, 5.0 }, 0);
            cr.LineWidth = 1.0;

            if (isDarkMode)
            {
                cr.SetSourceRGBA(1.0, 1.0, 1.0, 0.05);
            }
            else
            {
                cr.SetSourceRGBA(0.0, 0.0, 0.0, 0.06);
            }

            double midY = paddingY + (plotHeight * 0.5);
            cr.MoveTo(paddingX, midY);
            cr.LineTo(paddingX + plotWidth, midY);
            cr.Stroke();

            double topY = paddingY + 2;
            cr.MoveTo(paddingX, topY);
            cr.LineTo(paddingX + plotWidth, topY);
            cr.Stroke();
            cr.Restore();
        }

        // Computes smoothed spline points and renders the gradient area and stroke
        private void DrawWaveform(Context cr, double paddingX, double paddingY, double plotWidth, double plotHeight, double bottomY)
        {
            var points = new PointD[SampleCount];
            double stepX = plotWidth / (SampleCount - 1);

            for (int i = 0; i < SampleCount; i++)
            {
                int sampleIdx = (currentIndex + i) % SampleCount;
                double val = samples[sampleIdx];
                double ratio = Math.Clamp(val / maxSpeed, 0.0, 1.0);
                double px = paddingX + (i * stepX);
                double py = bottomY - (ratio * plotHeight);
                points[i] = new PointD(px, py);
            }

            // Path 1: Closed polygon for gradient fill
            cr.MoveTo(points[0].X, bottomY);
            cr.LineTo(points[0].X, points[0].Y);

            for (int i = 0; i < SampleCount - 1; i++)
            {
                var p0 = i > 0 ? points[i - 1] : points[i];
                var p1 = points[i];
                var p2 = points[i + 1];
                var p3 = i < SampleCount - 2 ? points[i + 2] : p2;

                double cp1x = p1.X + (p2.X - p0.X) / 6.0;
                double cp1y = p1.Y + (p2.Y - p0.Y) / 6.0;
                double cp2x = p2.X - (p3.X - p1.X) / 6.0;
                double cp2y = p2.Y - (p3.Y - p1.Y) / 6.0;

                cr.CurveTo(cp1x, cp1y, cp2x, cp2y, p2.X, p2.Y);
            }

            cr.LineTo(points[SampleCount - 1].X, bottomY);
            cr.ClosePath();

            // Fill gradient (accent blue to transparent baseline)
            using (var gradient = new LinearGradient(0, paddingY, 0, bottomY))
            {
                gradient.AddColorStop(0.0, new Color(0.21, 0.52, 0.89, 0.42)); // #3584e4 @ 42%
                gradient.AddColorStop(0.7, new Color(0.21, 0.52, 0.89, 0.12));
                gradient.AddColorStop(1.0, new Color(0.21, 0.52, 0.89, 0.00));
                cr.SetSource(gradient);
                cr.Fill();
            }

            // Path 2: Glowing upper stroke line
            cr.MoveTo(points[0].X, points[0].Y);
            for (int i = 0; i < SampleCount - 1; i++)
            {
                var p0 = i > 0 ? points[i - 1] : points[i];
                var p1 = points[i];
                var p2 = points[i + 1];
                var p3 = i < SampleCount - 2 ? points[i + 2] : p2;

                double cp1x = p1.X + (p2.X - p0.X) / 6.0;
                double cp1y = p1.Y + (p2.Y - p0.Y) / 6.0;
                double cp2x = p2.X - (p3.X - p1.X) / 6.0;
                double cp2y = p2.Y - (p3.Y - p1.Y) / 6.0;

                cr.CurveTo(cp1x, cp1y, cp2x, cp2y, p2.X, p2.Y);
            }

            cr.SetSourceRGBA(0.21, 0.52, 0.89, 0.95);
            cr.LineWidth = 2.2;
            cr.Stroke();

            // Render current active pulse dot on latest sample point
            var lastPoint = points[SampleCount - 1];
            if (currentSpeed > 0)
            {
                // Outer glow ring
                cr.Arc(lastPoint.X, lastPoint.Y, 5.0, 0, Math.PI * 2);
                cr.SetSourceRGBA(0.21, 0.52, 0.89, 0.35);
                cr.Fill();

                // Center bright dot
                cr.Arc(lastPoint.X, lastPoint.Y, 2.5, 0, Math.PI * 2);
                cr.SetSourceRGBA(0.0, 0.82, 1.0, 1.0); // Cyan #00d2ff
                cr.Fill();
            }
        }

        // Renders clean typography badges for current and peak speeds
        private void DrawMetricsBadges(Context cr, int width, double paddingX, double paddingY)
        {
            cr.SelectFontFace("Cantarell", FontSlant.Normal, FontWeight.Bold);

            // Left badge: Speed / Network Activity Title
            cr.SetFontSize(11.0);
            if (isDarkMode)
            {
                cr.SetSourceRGBA(0.75, 0.75, 0.75, 0.9);
            }
            else
            {
                cr.SetSourceRGBA(0.3, 0.3, 0.3, 0.9);
            }
            cr.MoveTo(paddingX + 4, paddingY + 12);
            cr.ShowText(currentSpeed > 0 ? "⚡ Network Throughput" : "💤 Idle");

            // Right badge: Current Speed + Peak Speed
            string speedText = currentSpeed > 0
                ? $"{FormattingHelper.FormatSize((long)currentSpeed)}/s"
                : "0 B/s";
            string peakText = peakSpeed > 0
                ? $"Peak: {FormattingHelper.FormatSize((long)peakSpeed)}/s"
                : "";

            cr.SetFontSize(12.0);
            cr.SetSourceRGBA(0.21, 0.52, 0.89, 1.0); // Accent blue
            var extents = cr.TextExtents(speedText);
            double speedX = width - paddingX - extents.Width - 6;
            cr.MoveTo(speedX, paddingY + 12);
            cr.ShowText(speedText);

            if (!string.IsNullOrEmpty(peakText))
            {
                cr.SelectFontFace("Cantarell", FontSlant.Normal, FontWeight.Normal);
                cr.SetFontSize(10.0);
                if (isDarkMode)
                {
                    cr.SetSourceRGBA(0.55, 0.55, 0.55, 0.85);
                }
                else
                {
                    cr.SetSourceRGBA(0.45, 0.45, 0.45, 0.85);
                }
                var peakExtents = cr.TextExtents(peakText);
                cr.MoveTo(speedX - peakExtents.Width - 14, paddingY + 12);
                cr.ShowText(peakText);
            }
        }

        // Helper to trace rounded rectangle geometry
        private static void DrawRoundedRectangle(Context cr, double x, double y, double w, double h, double r)
        {
            cr.NewPath();
            cr.Arc(x + w - r, y + r, r, -Math.PI / 2, 0);
            cr.Arc(x + w - r, y + h - r, r, 0, Math.PI / 2);
            cr.Arc(x + r, y + h - r, r, Math.PI / 2, Math.PI);
            cr.Arc(x + r, y + r, r, Math.PI, 3 * Math.PI / 2);
            cr.ClosePath();
        }
    }
}
