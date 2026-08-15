using GestureSign.Common.Applications;
using GestureSign.Common.Input;
using GestureSign.Common.Log;
using GestureSign.Daemon.Input;
using GestureSign.PointPatterns;
using ManagedWinapi.Hooks;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace GestureSign.Daemon.Triggers
{
    class TouchPadEdgeTrigger : Trigger
    {
        public const string TopGestureName = "TouchPadEdge.Top";
        public const string BottomGestureName = "TouchPadEdge.Bottom";
        public const string LeftGestureName = "TouchPadEdge.Left";
        public const string RightGestureName = "TouchPadEdge.Right";
        public const string TopLeftGestureName = "TouchPadEdge.Top.Left";
        public const string TopRightGestureName = "TouchPadEdge.Top.Right";
        public const string BottomLeftGestureName = "TouchPadEdge.Bottom.Left";
        public const string BottomRightGestureName = "TouchPadEdge.Bottom.Right";
        public const string LeftUpGestureName = "TouchPadEdge.Left.Up";
        public const string LeftDownGestureName = "TouchPadEdge.Left.Down";
        public const string RightUpGestureName = "TouchPadEdge.Right.Up";
        public const string RightDownGestureName = "TouchPadEdge.Right.Down";
        public const string LeftInwardGestureName = "TouchPadEdge.Left.Right";
        public const string RightInwardGestureName = "TouchPadEdge.Right.Left";

        private const int EdgePercent = 8;
        private const int MaxTapTravel = 35;
        private const int MinSwipeTravel = 90;
        private const int CaptionButtonWidth = 180;
        private const int CaptionButtonHeight = 72;
        private readonly Devices _sourceDevice;
        private readonly string _gesturePrefix;
        private readonly string _logPrefix;
        private readonly int _edgePercent;
        private readonly int _maxTapTravel;
        private readonly int _minSwipeTravel;
        private readonly double _swipeDominanceRatio;
        private readonly bool _allowCornerEdges;
        private readonly bool _allowOppositeEdgeFallback;
        private PendingEdgeTrigger _pendingEdgeTrigger;

        public TouchPadEdgeTrigger()
            : this(Devices.TouchPad, "TouchPadEdge", "TouchPad", EdgePercent, MaxTapTravel, MinSwipeTravel, 1.5, false, false)
        {
        }

        public TouchPadEdgeTrigger(Devices sourceDevice, string gesturePrefix, string logPrefix)
            : this(sourceDevice, gesturePrefix, logPrefix, EdgePercent, MaxTapTravel, MinSwipeTravel, 1.5, false, false)
        {
        }

        public TouchPadEdgeTrigger(Devices sourceDevice, string gesturePrefix, string logPrefix, int edgePercent, int maxTapTravel, int minSwipeTravel, double swipeDominanceRatio, bool allowCornerEdges)
            : this(sourceDevice, gesturePrefix, logPrefix, edgePercent, maxTapTravel, minSwipeTravel, swipeDominanceRatio, allowCornerEdges, false)
        {
        }

        public TouchPadEdgeTrigger(Devices sourceDevice, string gesturePrefix, string logPrefix, int edgePercent, int maxTapTravel, int minSwipeTravel, double swipeDominanceRatio, bool allowCornerEdges, bool allowOppositeEdgeFallback)
        {
            _sourceDevice = sourceDevice;
            _gesturePrefix = gesturePrefix;
            _logPrefix = logPrefix;
            _edgePercent = edgePercent;
            _maxTapTravel = maxTapTravel;
            _minSwipeTravel = minSwipeTravel;
            _swipeDominanceRatio = swipeDominanceRatio;
            _allowCornerEdges = allowCornerEdges;
            _allowOppositeEdgeFallback = allowOppositeEdgeFallback;
            PointCapture.Instance.CaptureStarted += PointCapture_CaptureStarted;
            PointCapture.Instance.BeforePointsCaptured += PointCapture_BeforePointsCaptured;
            if (_sourceDevice == Devices.TouchPad)
            {
                PointCapture.Instance.MouseHook.MouseMove += CursorFreezer.OnMouseMove;
                PointCapture.Instance.MouseHook.MouseDown += CursorFreezer.OnMouseDown;
                PointCapture.Instance.CaptureEnded += (s, args) => CursorFreezer.Release(false);
                PointCapture.Instance.CaptureCanceled += (s, args) => CursorFreezer.Release(false);
            }
        }

        private void PointCapture_CaptureStarted(object sender, PointsCapturedEventArgs e)
        {
            _pendingEdgeTrigger = null;

            var pointCapture = PointCapture.Instance;
            if (pointCapture.Mode == CaptureMode.Training || pointCapture.SourceDevice != _sourceDevice)
                return;

            if (e.Points == null || e.Points.Count != 1 || e.Points[0].Count == 0)
                return;

            var edge = GetStartEdge(e.Points[0].First());
            if (edge == null)
            {
                Logging.LogMessage($"{_logPrefix} edge capture ignored. Reason=NotOnEdge, Point={FormatPoint(e.Points[0].First())}");
                return;
            }

            ApplicationManager.Instance.GetForegroundApplications();
            var actionEdge = GetActionEdge(edge.Value);
            var hasAnyAction = GetCandidateGestureNames(actionEdge)
                .Any(name => ApplicationManager.Instance.GetRecognizedDefinedAction(name)?.Any() == true);
            if (!hasAnyAction)
            {
                Logging.LogMessage($"{_logPrefix} edge capture ignored. Reason=NoAction, Edge={edge}, Point={FormatPoint(e.Points[0].First())}");
                return;
            }

            _pendingEdgeTrigger = new PendingEdgeTrigger(actionEdge, e.FirstCapturedPoints.FirstOrDefault());
            e.Cancel = false;
            e.ForceCapture = true;
            e.BlockTouchInputThreshold = 0;
            if (_sourceDevice == Devices.TouchPad)
                CursorFreezer.Freeze(edge.Value, GetBoundFreezeDirections(actionEdge));
            Logging.LogMessage($"{_logPrefix} edge capture accepted. Edge={edge}, ActionEdge={actionEdge}, Point={FormatPoint(e.Points[0].First())}");
        }

        private void PointCapture_BeforePointsCaptured(object sender, PointsCapturedEventArgs e)
        {
            var pointCapture = PointCapture.Instance;
            if (pointCapture.Mode == CaptureMode.Training || pointCapture.SourceDevice != _sourceDevice)
                return;

            // This handler also runs on the idle-release timer thread, so PointCapture_CaptureStarted
            // can clear _pendingEdgeTrigger from the input thread part way through. Take the pending
            // trigger once and work from the local copy, otherwise a later read can be null.
            var pendingEdgeTrigger = Interlocked.Exchange(ref _pendingEdgeTrigger, null);
            if (pendingEdgeTrigger != null)
            {
                var pendingGestureName = e.Points == null || e.Points.Count != 1 || e.Points[0].Count == 0
                    ? null
                    : GetEdgeGestureName(pendingEdgeTrigger.Edge, e.Points[0]);
                if (pendingGestureName == null)
                {
                    Logging.LogMessage($"{_logPrefix} edge trigger canceled. Edge={pendingEdgeTrigger.Edge}, Reason=NoTapOrSwipe");
                    CursorFreezer.Release(false);
                    return;
                }

                ApplicationManager.Instance.GetForegroundApplications();
                var pendingActions = ApplicationManager.Instance.GetRecognizedDefinedAction(pendingGestureName)?.ToList();
                if (pendingActions == null || pendingActions.Count == 0)
                {
                    Logging.LogMessage($"{_logPrefix} edge trigger canceled. Edge={pendingGestureName}, Reason=NoAction");
                    CursorFreezer.Release(false);
                    return;
                }

                Logging.LogMessage($"{_logPrefix} edge trigger fired. Edge={pendingGestureName}, Actions={pendingActions.Count}");
                e.Cancel = true;
                CursorFreezer.Release(false);
                OnTriggerFired(new TriggerFiredEventArgs(pendingActions, pendingEdgeTrigger.FiredPoint, ClonePoints(e.Points)));
                return;
            }

            if (e.Points == null || e.Points.Count == 0 || e.Points[0] == null || e.Points[0].Count == 0)
                return;

            var edgeGestureName = GetEdgeGestureName(e.Points[0]);
            if (edgeGestureName == null)
                return;

            ApplicationManager.Instance.GetForegroundApplications();
            var actions = ApplicationManager.Instance.GetRecognizedDefinedAction(edgeGestureName)?.ToList();
            if (actions == null || actions.Count == 0)
                return;

            Logging.LogMessage($"{_logPrefix} edge trigger fired. Edge={edgeGestureName}, Actions={actions.Count}");
            e.Cancel = true;
            OnTriggerFired(new TriggerFiredEventArgs(actions, e.FirstCapturedPoints.FirstOrDefault(), ClonePoints(e.Points)));
        }

        private static List<List<Point>> ClonePoints(IEnumerable<List<Point>> points)
        {
            return points?.Select(stroke => stroke?.ToList() ?? new List<Point>()).ToList();
        }

        private string GetEdgeGestureName(List<Point> points)
        {
            var edge = GetStartEdge(points.First());
            return edge == null ? null : GetEdgeGestureName(edge.Value, points);
        }

        private Edge? GetStartEdge(Point start)
        {
            var bounds = Screen.FromPoint(start).Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return null;

            var x = start.X - bounds.Left;
            var y = start.Y - bounds.Top;
            var edgeWidth = Math.Max(1, bounds.Width * _edgePercent / 100);
            var edgeHeight = Math.Max(1, bounds.Height * _edgePercent / 100);

            var top = y <= edgeHeight;
            var bottom = y >= bounds.Height - edgeHeight;
            var left = x <= edgeWidth;
            var right = x >= bounds.Width - edgeWidth;

            if ((_sourceDevice == Devices.TouchScreen || _sourceDevice == Devices.Mouse) && IsCaptionButtonRegion(bounds, x, y))
            {
                Logging.LogMessage($"{_logPrefix} edge capture ignored. Reason=CaptionButtonRegion, Point={FormatPoint(start)}");
                return null;
            }

            if (top && !left && !right)
                return Edge.Top;
            if (bottom && !left && !right)
                return Edge.Bottom;
            if (left && !top && !bottom)
                return Edge.Left;
            if (right && !top && !bottom)
                return Edge.Right;

            if (_allowCornerEdges)
            {
                if (top && left)
                    return y <= x ? Edge.Top : Edge.Left;
                if (top && right)
                    return y <= bounds.Width - x ? Edge.Top : Edge.Right;
                if (bottom && left)
                    return bounds.Height - y <= x ? Edge.Bottom : Edge.Left;
                if (bottom && right)
                    return bounds.Height - y <= bounds.Width - x ? Edge.Bottom : Edge.Right;
            }

            return null;
        }

        private string GetEdgeGestureName(Edge edge, List<Point> points)
        {
            var start = points.First();
            var end = points.Last();
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            if (PointPatternMath.GetDistance(start, end) <= _maxTapTravel)
                return GetTapGestureName(edge);

            switch (edge)
            {
                case Edge.Top:
                    if (IsHorizontalSwipe(dx, dy))
                        return dx < 0 ? $"{_gesturePrefix}.Top.Left" : $"{_gesturePrefix}.Top.Right";
                    break;
                case Edge.Bottom:
                    if (IsHorizontalSwipe(dx, dy))
                        return dx < 0 ? $"{_gesturePrefix}.Bottom.Left" : $"{_gesturePrefix}.Bottom.Right";
                    break;
                case Edge.Left:
                    if (IsVerticalSwipe(dx, dy))
                        return dy < 0 ? $"{_gesturePrefix}.Left.Up" : $"{_gesturePrefix}.Left.Down";
                    // Swipe inward (rightward) from the left edge.
                    if (IsHorizontalSwipe(dx, dy) && dx > 0)
                        return $"{_gesturePrefix}.Left.Right";
                    break;
                case Edge.Right:
                    if (IsVerticalSwipe(dx, dy))
                        return dy < 0 ? $"{_gesturePrefix}.Right.Up" : $"{_gesturePrefix}.Right.Down";
                    // Swipe inward (leftward) from the right edge.
                    if (IsHorizontalSwipe(dx, dy) && dx < 0)
                        return $"{_gesturePrefix}.Right.Left";
                    break;
            }

            return null;
        }

        private bool IsHorizontalSwipe(int dx, int dy)
        {
            return Math.Abs(dx) >= _minSwipeTravel && Math.Abs(dx) > Math.Abs(dy) * _swipeDominanceRatio;
        }

        private bool IsVerticalSwipe(int dx, int dy)
        {
            return Math.Abs(dy) >= _minSwipeTravel && Math.Abs(dy) > Math.Abs(dx) * _swipeDominanceRatio;
        }

        private string GetTapGestureName(Edge edge)
        {
            switch (edge)
            {
                case Edge.Top:
                    return $"{_gesturePrefix}.Top";
                case Edge.Bottom:
                    return $"{_gesturePrefix}.Bottom";
                case Edge.Left:
                    return $"{_gesturePrefix}.Left";
                case Edge.Right:
                    return $"{_gesturePrefix}.Right";
                default:
                    return null;
            }
        }

        private IEnumerable<string> GetCandidateGestureNames(Edge edge)
        {
            yield return GetTapGestureName(edge);
            switch (edge)
            {
                case Edge.Top:
                    yield return $"{_gesturePrefix}.Top.Left";
                    yield return $"{_gesturePrefix}.Top.Right";
                    break;
                case Edge.Bottom:
                    yield return $"{_gesturePrefix}.Bottom.Left";
                    yield return $"{_gesturePrefix}.Bottom.Right";
                    break;
                case Edge.Left:
                    yield return $"{_gesturePrefix}.Left.Up";
                    yield return $"{_gesturePrefix}.Left.Down";
                    yield return $"{_gesturePrefix}.Left.Right";
                    break;
                case Edge.Right:
                    yield return $"{_gesturePrefix}.Right.Up";
                    yield return $"{_gesturePrefix}.Right.Down";
                    yield return $"{_gesturePrefix}.Right.Left";
                    break;
            }
        }

        private Edge GetActionEdge(Edge edge)
        {
            if (!_allowOppositeEdgeFallback)
                return edge;

            if (HasAnyAction(edge))
                return edge;

            var opposite = GetOppositeEdge(edge);
            if (HasAnyAction(opposite))
            {
                Logging.LogMessage($"{_logPrefix} edge action fallback. RawEdge={edge}, ActionEdge={opposite}");
                return opposite;
            }

            return edge;
        }

        private bool HasAnyAction(Edge edge)
        {
            return GetCandidateGestureNames(edge)
                .Any(name => ApplicationManager.Instance.GetRecognizedDefinedAction(name)?.Any() == true);
        }

        private FreezeDirections GetBoundFreezeDirections(Edge edge)
        {
            var directions = FreezeDirections.None;
            switch (edge)
            {
                case Edge.Left:
                    if (HasBoundAction($"{_gesturePrefix}.Left.Up")) directions |= FreezeDirections.Up;
                    if (HasBoundAction($"{_gesturePrefix}.Left.Down")) directions |= FreezeDirections.Down;
                    if (HasBoundAction($"{_gesturePrefix}.Left.Right")) directions |= FreezeDirections.Inward;
                    break;
                case Edge.Right:
                    if (HasBoundAction($"{_gesturePrefix}.Right.Up")) directions |= FreezeDirections.Up;
                    if (HasBoundAction($"{_gesturePrefix}.Right.Down")) directions |= FreezeDirections.Down;
                    if (HasBoundAction($"{_gesturePrefix}.Right.Left")) directions |= FreezeDirections.Inward;
                    break;
                case Edge.Top:
                    if (HasBoundAction($"{_gesturePrefix}.Top.Left")) directions |= FreezeDirections.AlongLeft;
                    if (HasBoundAction($"{_gesturePrefix}.Top.Right")) directions |= FreezeDirections.AlongRight;
                    break;
                case Edge.Bottom:
                    if (HasBoundAction($"{_gesturePrefix}.Bottom.Left")) directions |= FreezeDirections.AlongLeft;
                    if (HasBoundAction($"{_gesturePrefix}.Bottom.Right")) directions |= FreezeDirections.AlongRight;
                    break;
            }
            return directions;
        }

        private static bool HasBoundAction(string gestureName)
        {
            return ApplicationManager.Instance.GetRecognizedDefinedAction(gestureName)?.Any() == true;
        }

        private static Edge GetOppositeEdge(Edge edge)
        {
            switch (edge)
            {
                case Edge.Top:
                    return Edge.Bottom;
                case Edge.Bottom:
                    return Edge.Top;
                case Edge.Left:
                    return Edge.Right;
                case Edge.Right:
                    return Edge.Left;
                default:
                    return edge;
            }
        }

        private static string FormatPoint(Point point)
        {
            return $"{point.X},{point.Y}";
        }

        private static bool IsCaptionButtonRegion(Rectangle bounds, int x, int y)
        {
            return y <= CaptionButtonHeight &&
                   (x <= CaptionButtonWidth || x >= bounds.Width - CaptionButtonWidth);
        }

        private enum Edge
        {
            Top,
            Bottom,
            Left,
            Right
        }

        [Flags]
        private enum FreezeDirections
        {
            None = 0,
            Up = 1 << 0,
            Down = 1 << 1,
            Inward = 1 << 2,
            AlongLeft = 1 << 3,
            AlongRight = 1 << 4
        }

        /// <summary>
        /// Pins the cursor while a touchpad edge gesture is in flight by swallowing
        /// mouse moves in the low-level hook. If the motion stops matching any bound
        /// gesture direction, the freeze is released and the swallowed travel is
        /// re-applied so ordinary pointing that starts on an edge loses nothing.
        /// </summary>
        private static class CursorFreezer
        {
            private const int DirectionSlop = 25;
            private const int VerticalSlop = 35;
            private const double DominanceRatio = 1.5;
            private const int WatchdogMs = 2500;

            private static readonly object SyncRoot = new object();
            private static System.Threading.Timer _watchdog;
            private static bool _frozen;
            private static Edge _edge;
            private static FreezeDirections _directions;
            private static Point _anchor;
            private static int _sumX;
            private static int _sumY;

            public static void Freeze(Edge edge, FreezeDirections directions)
            {
                lock (SyncRoot)
                {
                    _edge = edge;
                    _directions = directions;
                    _anchor = Cursor.Position;
                    _sumX = _sumY = 0;
                    _frozen = true;
                    // A leaked freeze would deaden the pointer entirely, so force a
                    // release even if every regular release path is missed.
                    if (_watchdog == null)
                        _watchdog = new System.Threading.Timer(o => Release(false), null, WatchdogMs, System.Threading.Timeout.Infinite);
                    else
                        _watchdog.Change(WatchdogMs, System.Threading.Timeout.Infinite);
                }
                Logging.LogMessage($"TouchPad edge cursor freeze engaged. Edge={edge}, Directions={directions}");
            }

            public static void Release(bool restore)
            {
                Point target;
                lock (SyncRoot)
                {
                    if (!_frozen)
                        return;
                    _frozen = false;
                    _watchdog?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                    target = new Point(_anchor.X + _sumX, _anchor.Y + _sumY);
                }
                if (restore)
                    Cursor.Position = target;
                Logging.LogMessage($"TouchPad edge cursor freeze released. Restore={restore}");
            }

            public static void OnMouseMove(LowLevelMouseMessage evt, ref bool handled)
            {
                bool abort;
                lock (SyncRoot)
                {
                    if (!_frozen)
                        return;
                    // The cursor stays pinned at the anchor, so each hook event
                    // carries only its own ballistic delta relative to it; the sum
                    // is the travel the pointer would have made.
                    _sumX += evt.Point.X - _anchor.X;
                    _sumY += evt.Point.Y - _anchor.Y;
                    handled = true;
                    abort = !StillLooksLikeBoundSwipe();
                }
                if (abort)
                    Release(true);
            }

            public static void OnMouseDown(LowLevelMouseMessage evt, ref bool handled)
            {
                Release(false);
            }

            private static bool StillLooksLikeBoundSwipe()
            {
                int absX = Math.Abs(_sumX);
                int absY = Math.Abs(_sumY);
                if (absX <= DirectionSlop && absY <= VerticalSlop)
                    return true;

                if (absY > absX * DominanceRatio)
                {
                    var direction = _sumY < 0 ? FreezeDirections.Up : FreezeDirections.Down;
                    return (_directions & direction) != 0;
                }

                if (absX > absY * DominanceRatio)
                {
                    switch (_edge)
                    {
                        case Edge.Right:
                            return _sumX < 0 && (_directions & FreezeDirections.Inward) != 0;
                        case Edge.Left:
                            return _sumX > 0 && (_directions & FreezeDirections.Inward) != 0;
                        default:
                            var along = _sumX < 0 ? FreezeDirections.AlongLeft : FreezeDirections.AlongRight;
                            return (_directions & along) != 0;
                    }
                }

                // A wandering diagonal is pointer movement, not a gesture.
                return false;
            }
        }

        private class PendingEdgeTrigger
        {
            public PendingEdgeTrigger(Edge edge, Point firedPoint)
            {
                Edge = edge;
                FiredPoint = firedPoint;
            }

            public Edge Edge { get; }
            public Point FiredPoint { get; }
        }
    }
}
