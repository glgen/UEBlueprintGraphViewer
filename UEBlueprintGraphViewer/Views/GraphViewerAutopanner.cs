using Avalonia;
using Avalonia.Controls;
using System;

namespace UEBlueprintGraphViewer.Views
{
    public class GraphViewerAutopanner
    {
        private readonly GraphView2 _view;

        private Point _from;
        private Point _to;
        private double _duration;
        private double _elapsed;

        private TopLevel? _topLevel;
        private TimeSpan? _lastUpdate;

        public GraphViewerAutopanner(GraphView2 view)
        {
            _view = view;
            if (TopLevel.GetTopLevel(App.MainWindow) is {} top)
                _topLevel = top;
        }

        public void PanToCentered(Point to)
        {
            double x = to.X - _view.Bounds.Width / _view.Scaling / 4;
            double y = to.Y - (_view.Bounds.Height / _view.Scaling / 2 - 40);
            StartPanningAnimation(new Point(_view.Translation.X, _view.Translation.Y), new Point(x, y) * _view.Scaling);
        }
        
        private void StartPanningAnimation(Point from, Point to)
        {
            double time = Math.Min(1000, Point.Distance(from, to) / 2.5);

            if (time < 1)
                return;

            _from = from;
            _to = to;
            _duration = time;
            _elapsed = 0;
            _lastUpdate = null;

            _view.DisableMoving = true;

            _topLevel?.RequestAnimationFrame((time) =>
            {
                _lastUpdate = time;
                HandleAutoPanning(time);
            });
        }

        private void HandleAutoPanning(TimeSpan currentTime)
        {
            double f = (_elapsed / _duration) - 1d;
            double progress = f * f * f + 1d;

            Point newPoint = ((_to - _from) * progress) + _from;
            _view.SetTranslation(newPoint);

            _elapsed += (currentTime - _lastUpdate)?.Milliseconds ?? 0;
            _lastUpdate = currentTime;

            if (progress < 1)
            {
                _topLevel?.RequestAnimationFrame(HandleAutoPanning);
            }
            else
            {
                _view.DisableMoving = false;
            }
        }
    }
}
