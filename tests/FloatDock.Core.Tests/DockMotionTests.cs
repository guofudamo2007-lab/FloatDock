using FloatDock.Core;

internal static class DockMotionTests
{
    internal static void Run()
    {
        foreach (var size in new[] { 32d, 40, 64 })
        {
            var stride = size + 16;
            var centers = Enumerable.Range(0, 5).Select(i => i * stride + stride / 2).ToArray();
            foreach (var pointer in Enumerable.Range(0, 41).Select(i => i * stride / 8))
            {
                var shifts = DockModel.NeighborShifts(centers, pointer, size, false);
                var firstHalf = DockModel.Motion(centers[0] - pointer, false, false).Scale * size / 2;
                var lastHalf = DockModel.Motion(centers[^1] - pointer, false, false).Scale * size / 2;
                Check(24 + centers[0] + shifts[0] - firstHalf >= 0, "First icon clips its animation gutter.");
                Check(centers[^1] + shifts[^1] + lastHalf <= centers.Length * stride + 24, "Last icon clips its animation gutter.");
                for (var i = 1; i < centers.Length; i++)
                {
                    var left = DockModel.Motion(centers[i - 1] - pointer, false, false).Scale * size / 2;
                    var right = DockModel.Motion(centers[i] - pointer, false, false).Scale * size / 2;
                    Check(centers[i] + shifts[i] - right >= centers[i - 1] + shifts[i - 1] + left, "Magnified icons overlap.");
                }
            }
            var middle = DockModel.NeighborShifts(centers, centers[2], size, false);
            Check(Math.Abs(middle[0] + middle[4]) < .00001 && Math.Abs(middle[2]) < .00001, "Centered hover must remain symmetric.");
            Check(DockModel.NeighborShifts(centers, double.PositiveInfinity, size, false).All(x => x == 0), "Pointer leave must reset displacement.");
            Check(DockModel.NeighborShifts(centers, centers[2], size, true).All(x => x == 0), "Reduced motion must reset displacement.");
        }
        Check(DockModel.Motion(double.NaN, false, false) == new MotionTarget(1, 0), "Invalid pointer must be neutral.");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
