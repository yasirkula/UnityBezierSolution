namespace BezierSolution
{
	public interface IBezierWalker
	{
		BezierSpline Spline { get; }
		float NormalizedT { get; }
		bool MovingForward { get; }
	}
}