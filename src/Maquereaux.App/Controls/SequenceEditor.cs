using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Maquereaux.App.Services;
using Maquereaux.App.ViewModels;
using Maquereaux.Win32;

namespace Maquereaux.App.Controls;

/// <summary>Conteneur d'une touche de la séquence : focusable (sélection au clavier).</summary>
public sealed class SequenceKeyItem : ContentControl
{
    static SequenceKeyItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SequenceKeyItem), new FrameworkPropertyMetadata(typeof(SequenceKeyItem)));
        FocusableProperty.OverrideMetadata(typeof(SequenceKeyItem), new FrameworkPropertyMetadata(true));
    }
}

/// <summary>
/// Éditeur de séquence en « keycaps » (DataContext = <see cref="ProfileViewModel"/>).
/// <list type="bullet">
/// <item>Glisser-déposer pour réordonner, avec indicateur d'insertion et animation fluide (technique FLIP).</item>
/// <item>Clavier : ←/→ pour naviguer, Suppr pour supprimer, Ctrl+D pour dupliquer, Alt+←/→ (ou ↑/↓) pour déplacer,
/// Entrée/F2 pour remplacer la touche.</item>
/// <item>Mode capture : « Appuie sur une touche… » (Échap annule) — raccourcis globaux suspendus pendant ce temps.</item>
/// </list>
/// </summary>
public sealed class SequenceEditor : ItemsControl
{
    private static readonly DependencyPropertyKey IsAddingPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsAdding), typeof(bool), typeof(SequenceEditor), new PropertyMetadata(false));

    public static readonly DependencyProperty IsAddingProperty = IsAddingPropertyKey.DependencyProperty;

    private Point _dragStart;
    private SequenceKeyItem? _pressedItem;
    private bool _dragging;
    private int _dropIndex = -1;
    private InsertionAdorner? _insertion;
    private StepViewModel? _capturingStep;

    public SequenceEditor()
    {
        Focusable = false;
        AllowDrop = false;
        LostKeyboardFocus += (_, e) =>
        {
            // Capture annulée si le focus quitte l'éditeur
            if (!IsKeyboardFocusWithin) EndCapture();
        };
    }

    /// <summary>Vrai pendant la capture d'une nouvelle touche (bouton « + »).</summary>
    public bool IsAdding => (bool)GetValue(IsAddingProperty);

    private ProfileViewModel? Profile => DataContext as ProfileViewModel;

    protected override DependencyObject GetContainerForItemOverride() => new SequenceKeyItem();

    protected override bool IsItemItsOwnContainerOverride(object item) => item is SequenceKeyItem;

    // ------------------------------------------------------------------ Capture

    /// <summary>Passe en mode « ajouter une touche » (appelé par le bouton +).</summary>
    public void BeginAdd()
    {
        EndCapture();
        SetValue(IsAddingPropertyKey, true);
        KeyCaptureCoordinator.Begin();
        Focusable = true;
        Focus();
    }

    /// <summary>Remplace la touche d'une étape (Entrée / F2 / double-clic).</summary>
    public void BeginReplace(StepViewModel step)
    {
        EndCapture();
        _capturingStep = step;
        step.IsCapturing = true;
        KeyCaptureCoordinator.Begin();
        (ItemContainerGenerator.ContainerFromItem(step) as UIElement)?.Focus();
    }

    private void EndCapture()
    {
        bool wasCapturing = IsAdding || _capturingStep is not null;
        SetValue(IsAddingPropertyKey, false);
        if (_capturingStep is not null) _capturingStep.IsCapturing = false;
        _capturingStep = null;
        Focusable = false;
        if (wasCapturing) KeyCaptureCoordinator.End();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var key = KeyCaptureHelper.RealKey(e);

        // --- Mode capture : la touche pressée devient une étape ---
        if (IsAdding || _capturingStep is not null)
        {
            e.Handled = true;
            if (key == System.Windows.Input.Key.Escape) { EndCapture(); return; }
            var vk = KeyCaptureHelper.ToVirtualKey(key);
            if (vk == 0) return;
            var stroke = KeyboardLayout.Describe(vk);
            if (_capturingStep is { } step)
            {
                step.Key = stroke;
                EndCapture();
                (ItemContainerGenerator.ContainerFromItem(step) as UIElement)?.Focus();
            }
            else
            {
                Profile?.AddStepCommand.Execute(stroke);
                EndCapture();
                FocusIndex(Items.Count - 1);
            }
            return;
        }

        // --- Raccourcis sur la touche sélectionnée ---
        if (Keyboard.FocusedElement is not SequenceKeyItem item || item.DataContext is not StepViewModel s || Profile is not { } p)
        {
            base.OnPreviewKeyDown(e);
            return;
        }
        int index = ItemContainerGenerator.IndexFromContainer(item);
        bool alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        switch (key)
        {
            case System.Windows.Input.Key.Delete:
            case System.Windows.Input.Key.Back:
                p.RemoveStepCommand.Execute(s);
                FocusIndex(Math.Min(index, Items.Count - 1));
                e.Handled = true;
                break;
            case System.Windows.Input.Key.D when ctrl:
                p.DuplicateStepCommand.Execute(s);
                FocusIndex(index + 1);
                e.Handled = true;
                break;
            case System.Windows.Input.Key.Left or System.Windows.Input.Key.Up when alt:
                AnimatedMove(index, index - 1);
                e.Handled = true;
                break;
            case System.Windows.Input.Key.Right or System.Windows.Input.Key.Down when alt:
                AnimatedMove(index, index + 1);
                e.Handled = true;
                break;
            case System.Windows.Input.Key.Left:
                FocusIndex(index - 1);
                e.Handled = true;
                break;
            case System.Windows.Input.Key.Right:
                FocusIndex(index + 1);
                e.Handled = true;
                break;
            case System.Windows.Input.Key.Enter:
            case System.Windows.Input.Key.F2:
                BeginReplace(s);
                e.Handled = true;
                break;
            default:
                base.OnPreviewKeyDown(e);
                break;
        }
    }

    // Empêche Alt/F10 d'activer le menu système pendant une capture
    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        if (IsAdding || _capturingStep is not null) e.Handled = true;
        base.OnPreviewKeyUp(e);
    }

    private void FocusIndex(int index)
    {
        if (index < 0 || index >= Items.Count) return;
        Dispatcher.BeginInvoke(() => (ItemContainerGenerator.ContainerFromIndex(index) as UIElement)?.Focus(),
            System.Windows.Threading.DispatcherPriority.Input);
    }

    // ------------------------------------------------------------------ Souris : sélection, double-clic, glisser-déposer

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        // Les boutons/puces internes gardent leur comportement normal
        if (FindAncestor<System.Windows.Controls.Primitives.ButtonBase>(e.OriginalSource as DependencyObject) is not null) return;
        _pressedItem = FindAncestor<SequenceKeyItem>(e.OriginalSource as DependencyObject);
        if (_pressedItem is null) return;
        _dragStart = e.GetPosition(this);
        _pressedItem.Focus();
        if (e.ClickCount == 2 && _pressedItem.DataContext is StepViewModel step)
        {
            BeginReplace(step);
            _pressedItem = null;
            e.Handled = true;
        }
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (_pressedItem is null || e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(this);
        if (!_dragging)
        {
            if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            _dragging = true;
            CaptureMouse();
            _pressedItem.Opacity = 0.35;
        }
        UpdateDropIndicator(pos);
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        if (_dragging && _pressedItem is not null)
        {
            int from = ItemContainerGenerator.IndexFromContainer(_pressedItem);
            int to = _dropIndex > from ? _dropIndex - 1 : _dropIndex;
            _pressedItem.Opacity = 1;
            ReleaseMouseCapture();
            RemoveInsertion();
            if (from >= 0 && to >= 0 && to != from) AnimatedMove(from, to);
            e.Handled = true;
        }
        _dragging = false;
        _pressedItem = null;
        _dropIndex = -1;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (!_dragging) return;
        if (_pressedItem is not null) _pressedItem.Opacity = 1;
        RemoveInsertion();
        _dragging = false;
        _pressedItem = null;
    }

    /// <summary>Index d'insertion = avant la première touche dont le centre est « après » le curseur (ordre de lecture).</summary>
    private void UpdateDropIndicator(Point pos)
    {
        int count = Items.Count;
        int index = count;
        Rect? anchor = null;
        bool after = false;
        for (int i = 0; i < count; i++)
        {
            if (ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement c) continue;
            var r = new Rect(c.TranslatePoint(new Point(0, 0), this), new Size(c.ActualWidth, c.ActualHeight));
            bool sameRow = pos.Y >= r.Top - 6 && pos.Y <= r.Bottom + 6;
            if ((sameRow && pos.X < r.Left + r.Width / 2) || pos.Y < r.Top - 6)
            {
                index = i;
                anchor = r;
                after = false;
                break;
            }
            anchor = r;
            after = true;
        }
        _dropIndex = index;
        if (anchor is not { } a) return;
        var x = after ? a.Right + 6 : a.Left - 6;
        var layer = AdornerLayer.GetAdornerLayer(this);
        if (layer is null) return;
        if (_insertion is null)
        {
            _insertion = new InsertionAdorner(this);
            layer.Add(_insertion);
        }
        _insertion.Update(new Point(x, a.Top + 4), a.Height - 12);
    }

    private void RemoveInsertion()
    {
        if (_insertion is null) return;
        AdornerLayer.GetAdornerLayer(this)?.Remove(_insertion);
        _insertion = null;
    }

    /// <summary>
    /// Déplacement animé (FLIP) : on mémorise la position de chaque touche, on déplace, puis chaque touche part de
    /// son ancienne position et glisse vers la nouvelle en 200 ms.
    /// </summary>
    private void AnimatedMove(int from, int to)
    {
        if (Profile is not { } p || to < 0 || to >= Items.Count || from == to) return;
        var before = new Dictionary<object, Point>();
        foreach (var item in Items)
            if (ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement c)
                before[item] = c.TranslatePoint(new Point(0, 0), this);

        var moved = Items[from];
        p.MoveStepCommand.Execute((from, to));
        UpdateLayout();

        if (Motion.Instance.Enabled)
        {
            foreach (var item in Items)
            {
                if (!before.TryGetValue(item, out var old) || ItemContainerGenerator.ContainerFromItem(item) is not FrameworkElement c) continue;
                var now = c.TranslatePoint(new Point(0, 0), this);
                var dx = old.X - now.X;
                var dy = old.Y - now.Y;
                if (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5) continue;
                var t = new TranslateTransform(dx, dy);
                c.RenderTransform = t;
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                var d = TimeSpan.FromMilliseconds(200);
                t.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(dx, 0, d) { EasingFunction = ease });
                t.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(dy, 0, d) { EasingFunction = ease });
            }
        }
        (ItemContainerGenerator.ContainerFromItem(moved) as UIElement)?.Focus();
    }

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d is not null)
        {
            if (d is T t) return t;
            d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        }
        return null;
    }

    /// <summary>Barre verticale turquoise indiquant où la touche sera déposée.</summary>
    private sealed class InsertionAdorner : Adorner
    {
        private Point _at;
        private double _height;

        public InsertionAdorner(UIElement adorned) : base(adorned) => IsHitTestVisible = false;

        public void Update(Point at, double height)
        {
            _at = at;
            _height = Math.Max(8, height);
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            var brush = TryFindResource("Accent") as Brush ?? Brushes.Turquoise;
            dc.DrawRoundedRectangle(brush, null, new Rect(_at.X - 1.5, _at.Y, 3, _height), 1.5, 1.5);
            dc.DrawEllipse(brush, null, new Point(_at.X, _at.Y), 4, 4);
        }
    }
}
