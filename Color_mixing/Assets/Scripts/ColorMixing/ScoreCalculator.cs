using System;
using UnityEngine;

// Port dari prototype Swift: ScoreCalculator.swift + ColorConverter.swift.
// Warna diubah ke CIELAB, dibandingkan dengan deltaE2000, lalu dikalibrasi menjadi persen.
public static class ScoreCalculator
{
    private const double DegToRad = Math.PI / 180.0;
    private const double ScoreMidpoint = 23.0;
    private const double NearDifferenceScale = 200.0;
    private const double ScoreCutoff = 100.0;
    private const double Epsilon = 216.0 / 24389.0;
    private const double Kappa = 24389.0 / 27.0;
    private static readonly double TwentyFiveToSeventh = Math.Pow(25.0, 7.0);

    // Persentase kemiripan dua warna (0-100).
    public static double MatchPercentage(Color first, Color second) =>
        MatchPercentage(ToLab(first), ToLab(second));

    public static double MatchPercentage((double l, double a, double b) lab1, (double l, double a, double b) lab2)
    {
        double distance = Math.Min(DeltaE2000(lab1, lab2), ScoreCutoff);
        double zeroPoint = Similarity(ScoreCutoff);
        return 100.0 * (Similarity(distance) - zeroPoint) / (1.0 - zeroPoint);
    }

    private static double Similarity(double distance) =>
        1.0 / (1.0 + distance / NearDifferenceScale + Math.Pow(distance / ScoreMidpoint, 4));

    public static (double l, double a, double b) ToLab(Color color)
    {
        double r = Linearize(color.r), g = Linearize(color.g), b = Linearize(color.b);

        double fx = LabFunction((0.4124564 * r + 0.3575761 * g + 0.1804375 * b) / 0.95046);
        double fy = LabFunction(0.2126729 * r + 0.7151522 * g + 0.0721750 * b);
        double fz = LabFunction((0.0193339 * r + 0.1191920 * g + 0.9503041 * b) / 1.08906);
        return (116.0 * fy - 16.0, 500.0 * (fx - fy), 200.0 * (fy - fz));
    }

    private static double Linearize(double c) =>
        c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

    private static double LabFunction(double v) =>
        v > Epsilon ? Math.Pow(v, 1.0 / 3.0) : (Kappa * v + 16.0) / 116.0;

    public static double DeltaE2000((double l, double a, double b) lab1, (double l, double a, double b) lab2)
    {
        if (lab1 == lab2) return 0.0;

        // 1. Sesuaikan a* memakai rata-rata chroma.
        double meanChroma = (Hypot(lab1.a, lab1.b) + Hypot(lab2.a, lab2.b)) / 2.0;
        double meanChroma7 = Math.Pow(meanChroma, 7);
        double adjustment = 0.5 * (1.0 - Math.Sqrt(meanChroma7 / (meanChroma7 + TwentyFiveToSeventh)));
        double a1 = lab1.a * (1.0 + adjustment);
        double a2 = lab2.a * (1.0 + adjustment);

        // 2. Chroma dan hue hasil penyesuaian.
        double c1 = Hypot(a1, lab1.b);
        double c2 = Hypot(a2, lab2.b);
        double h1 = HueDegrees(lab1.b, a1);
        double h2 = HueDegrees(lab2.b, a2);

        // 3. Selisih hue terpendek, kecuali salah satu chroma nol.
        double deltaL = lab2.l - lab1.l;
        double deltaC = c2 - c1;
        double deltaH = h2 - h1;
        if (c1 == 0.0 || c2 == 0.0) deltaH = 0.0;
        else if (deltaH > 180.0) deltaH -= 360.0;
        else if (deltaH < -180.0) deltaH += 360.0;
        double deltaHueTerm = 2.0 * Math.Sqrt(c1 * c2) * Math.Sin(deltaH * DegToRad / 2.0);

        // 4. Nilai rata-rata, bobot, dan rotasi hue.
        double meanL = (lab1.l + lab2.l) / 2.0;
        double meanC = (c1 + c2) / 2.0;
        double meanH;
        if (c1 == 0.0 || c2 == 0.0) meanH = h1 + h2;
        else if (Math.Abs(h1 - h2) <= 180.0) meanH = (h1 + h2) / 2.0;
        else if (h1 + h2 < 360.0) meanH = (h1 + h2 + 360.0) / 2.0;
        else meanH = (h1 + h2 - 360.0) / 2.0;

        double t = 1.0 - 0.17 * Math.Cos((meanH - 30.0) * DegToRad)
            + 0.24 * Math.Cos(2.0 * meanH * DegToRad)
            + 0.32 * Math.Cos((3.0 * meanH + 6.0) * DegToRad)
            - 0.20 * Math.Cos((4.0 * meanH - 63.0) * DegToRad);
        double offsetL = meanL - 50.0;
        double weightL = 1.0 + 0.015 * offsetL * offsetL / Math.Sqrt(20.0 + offsetL * offsetL);
        double weightC = 1.0 + 0.045 * meanC;
        double weightH = 1.0 + 0.015 * meanC * t;
        double meanC7 = Math.Pow(meanC, 7);
        double rotationChroma = 2.0 * Math.Sqrt(meanC7 / (meanC7 + TwentyFiveToSeventh));
        double angleOffset = (meanH - 275.0) / 25.0;
        double rotationAngle = 30.0 * Math.Exp(-angleOffset * angleOffset);
        double rotationTerm = -Math.Sin(2.0 * rotationAngle * DegToRad) * rotationChroma;

        // 5. Jarak akhir.
        double termL = deltaL / weightL;
        double termC = deltaC / weightC;
        double termH = deltaHueTerm / weightH;
        return Math.Sqrt(Math.Max(0.0, termL * termL + termC * termC + termH * termH + rotationTerm * termC * termH));
    }

    private static double Hypot(double x, double y) => Math.Sqrt(x * x + y * y);

    private static double HueDegrees(double b, double a)
    {
        double degrees = Math.Atan2(b, a) / DegToRad;
        return degrees < 0.0 ? degrees + 360.0 : degrees;
    }
}