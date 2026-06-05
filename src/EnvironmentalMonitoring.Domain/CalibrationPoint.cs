namespace EnvironmentalMonitoring.Domain;

public sealed record CalibrationPoint(
    int PointNumber,
    decimal RawValue,
    decimal ReferenceValue);

public static class CalibrationCalculator
{
    public static double Apply(
        double rawValue,
        decimal scale,
        decimal offset,
        IReadOnlyList<CalibrationPoint>? calibrationPoints)
    {
        if (TryApplyThreePoint(rawValue, calibrationPoints, out var calibratedValue))
        {
            return calibratedValue;
        }

        return (rawValue * (double)scale) + (double)offset;
    }

    private static bool TryApplyThreePoint(
        double rawValue,
        IReadOnlyList<CalibrationPoint>? calibrationPoints,
        out double calibratedValue)
    {
        calibratedValue = rawValue;

        if (calibrationPoints is null || calibrationPoints.Count < 3)
        {
            return false;
        }

        var points = calibrationPoints
            .OrderBy(point => point.RawValue)
            .Take(3)
            .ToArray();

        if (points.Length < 3
            || points[0].RawValue == points[1].RawValue
            || points[1].RawValue == points[2].RawValue)
        {
            return false;
        }

        var left = rawValue <= (double)points[1].RawValue ? points[0] : points[1];
        var right = rawValue <= (double)points[1].RawValue ? points[1] : points[2];
        calibratedValue = Interpolate(rawValue, left, right);
        return double.IsFinite(calibratedValue);
    }

    private static double Interpolate(
        double rawValue,
        CalibrationPoint left,
        CalibrationPoint right)
    {
        var rawLeft = (double)left.RawValue;
        var rawRight = (double)right.RawValue;
        var referenceLeft = (double)left.ReferenceValue;
        var referenceRight = (double)right.ReferenceValue;
        var ratio = (rawValue - rawLeft) / (rawRight - rawLeft);
        return referenceLeft + ((referenceRight - referenceLeft) * ratio);
    }
}
