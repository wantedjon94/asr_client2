using System;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

public class VoiceCircleVisualizer : Control
{
    private float currentLevel = 0f;    // Smoothed
    private float targetLevel = 0f;     // From mic input

    private readonly System.Windows.Forms.Timer refreshTimer;

    public VoiceCircleVisualizer()
    {
        DoubleBuffered = true;
        BackColor = Color.WhiteSmoke;

        refreshTimer = new System.Windows.Forms.Timer { Interval = 30 };
        refreshTimer.Tick += (s, e) =>
        {
            SmoothUpdate();
            Invalidate();
        };
        refreshTimer.Start();
    }

    public void UpdateLevel(float newLevel)
    {
        targetLevel = Math.Clamp(newLevel, 0f, 1f);
    }

    private void SmoothUpdate()
    {
        float smoothing = 0.1f; // Lower = smoother
        currentLevel += (targetLevel - currentLevel) * smoothing;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        // Compute circle size
        int minSize = 40;
        int maxSize = Math.Min(Width, Height);
        int diameter = minSize + (int)((maxSize - minSize) * (currentLevel * 3));

        diameter = Math.Min(diameter, maxSize);

        int x = (Width - diameter) / 2;
        int y = (Height - diameter) / 2;

        // Color: Green (quiet) to Red (loud)
        Color quietColor = Color.Goldenrod;
        Color loudColor = Color.Red;
        Color fillColor = InterpolateColor(quietColor, loudColor, currentLevel);

        // Glow effect
        DrawGlow(g, x, y, diameter, fillColor);

        // Inner circle
        using (var brush = new SolidBrush(fillColor))
        {
            g.FillEllipse(brush, x, y, diameter, diameter);
        }
    }

    private void DrawGlow(Graphics g, int x, int y, int size, Color baseColor)
    {
        int glowSize = 40;
        Rectangle glowRect = new Rectangle(x - glowSize / 2, y - glowSize / 2, size + glowSize, size + glowSize);

        using (GraphicsPath path = new GraphicsPath())
        {
            path.AddEllipse(glowRect);
            using (PathGradientBrush pgb = new PathGradientBrush(path))
            {
                pgb.CenterColor = Color.FromArgb(180, baseColor);
                pgb.SurroundColors = new[] { Color.FromArgb(0, baseColor) };
                g.FillEllipse(pgb, glowRect);
            }
        }
    }

    private Color InterpolateColor(Color from, Color to, float t)
    {
        int r = (int)(from.R + (to.R - from.R) * t);
        int g = (int)(from.G + (to.G - from.G) * t);
        int b = (int)(from.B + (to.B - from.B) * t);
        return Color.FromArgb(r, g, b);
    }
}
