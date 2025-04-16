using System;
using System.Drawing;
using System.Windows.Forms;

public class WaveformPainter : Control
{
    private float[] samples = new float[0];
    private readonly Pen waveformPen = new Pen(Color.Lime, 1);

    public void AddSamples(float[] newSamples)
    {
        samples = newSamples;
        Invalidate(); // triggers repaint
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (samples == null || samples.Length == 0) return;

        var g = e.Graphics;
        g.Clear(Color.Black);

        int midY = Height / 2;
        int len = samples.Length;
        float scaleX = (float)Width / len;

        for (int i = 1; i < len; i++)
        {
            float x1 = (i - 1) * scaleX;
            float y1 = midY - samples[i - 1] * midY;
            float x2 = i * scaleX;
            float y2 = midY - samples[i] * midY;

            g.DrawLine(waveformPen, x1, y1, x2, y2);
        }
    }
}
