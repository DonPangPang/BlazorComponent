﻿using BlazorComponent.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BlazorComponent;

public abstract class BMenuable : BActivatable, IMenuable, IAsyncDisposable
{
    private readonly int _stackMinZIndex = 6;
    private double _absoluteX;
    private double _absoluteY;
    private bool _hasWindow;
    private WindowAndDocument _windowAndDocument;

    [Parameter]
    public bool Absolute { get; set; }

    [Parameter]
    public bool AllowOverflow { get; set; }

    [Parameter]
    public string Attach { get; set; }

    [Parameter]
    public bool Bottom { get; set; }

    [Parameter]
    public string ContentClass { get; set; }

    [Parameter]
    public bool Left { get; set; }

    [Parameter]
    public StringNumber MaxWidth { get; set; }

    [Parameter]
    public StringNumber MinWidth { get; set; }

    [Parameter]
    public StringNumber NudgeBottom { get; set; }

    [Parameter]
    public StringNumber NudgeLeft { get; set; }

    [Parameter]
    public StringNumber NudgeRight { get; set; }

    [Parameter]
    public StringNumber NudgeTop { get; set; }

    [Parameter]
    public StringNumber NudgeWidth { get; set; }

    [Parameter]
    public bool OffsetOverflow { get; set; }

    [Parameter]
    public bool OffsetX { get; set; }

    [Parameter]
    public bool OffsetY { get; set; }

    [Parameter]
    public bool OpenOnClick { get; set; } = true;

    [Parameter]
    public double? PositionX { get; set; }

    [Parameter]
    public double? PositionY { get; set; }

    [Parameter]
    public bool Right { get; set; }

    [Parameter]
    public bool Top { get; set; }

    [Parameter]
    public override bool Value
    {
        get
        {
            return GetValue<bool>();
        }
        set
        {
            SetValue(value);
        }
    }

    [Parameter]
    public StringNumber ZIndex { get; set; }

    public override bool IsActive
    {
        get
        {
            return GetValue<bool>();
        }
        set
        {
            if (value && !Booted)
            {
                Booted = true;

                NextTick(async () =>
                {
                    await ShowLazyContent();

                    SetValue(value);
                    StateHasChanged();
                });
            }
            else
            {
                SetValue(value);
            }
        }
    }

    [Inject]
    public DomEventJsInterop DomEventJsInterop { get; set; }

    protected (Position activator, Position content) Dimensions = new(new Position(), new Position());

    protected bool ActivatorFixed { get; set; }

    protected double ComputedLeft
    {
        get
        {
            var a = Dimensions.activator;
            var c = Dimensions.content;
            var activatorLeft = Attach != null ? a.OffsetLeft : a.Left;
            var minWidth = Math.Max(a.Width, c.Width);

            double left = 0;
            left += Left ? activatorLeft - (minWidth - a.Width) : activatorLeft;

            if (OffsetX)
            {
                double maxWidth = 0;

                if (MaxWidth != null)
                {
                    (var isNumber, maxWidth) = MaxWidth.TryGetNumber();
                    maxWidth = isNumber ? Math.Min(a.Width, maxWidth) : a.Width;
                }

                left += Left ? -maxWidth : a.Width;
            }

            if (NudgeLeft != null)
            {
                var (_, nudgeLeft) = NudgeLeft.TryGetNumber();
                left -= nudgeLeft;
            }

            if (NudgeRight != null)
            {
                var (_, nudgeRight) = NudgeRight.TryGetNumber();
                left += nudgeRight;
            }

            return left;
        }
    }

    protected double ComputedTop
    {
        get
        {
            var a = Dimensions.activator;
            var c = Dimensions.content;

            double top = 0;

            if (Top) top += a.Height - c.Height;

            if (Attach != null)
                top += a.OffsetTop;
            else
                top += a.Top + PageYOffset;

            if (OffsetY) top += Top ? -a.Height : a.Height;

            if (NudgeTop != null)
            {
                var (isNumber, nudgeTop) = NudgeTop.TryGetNumber();
                if (isNumber)
                {
                    top -= nudgeTop;
                }
            }

            if (NudgeBottom != null)
            {
                var (isNumber, nudgeBottom) = NudgeBottom.TryGetNumber();
                if (isNumber)
                {
                    top += nudgeBottom;
                }
            }

            return top;
        }
    }

    protected int InternalZIndex { get; set; }

    protected double PageYOffset { get; set; }

    protected double PageWidth { get; set; }

    public ElementReference ContentRef { get; set; }

    public bool ShowContent { get; set; }

    public virtual string AttachedSelector => Attach;

    protected bool Booted { get; set; }

    protected override void OnWatcherInitialized()
    {
        base.OnWatcherInitialized();

        Watcher
            .Watch<bool>(nameof(IsActive), val =>
            {
                //REVIEW:
                if (Booted && val && Absolute)
                {
                    NextTick(async () =>
                    {
                        await UpdateDimensions();
                    });
                }
            });
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            _windowAndDocument = await RefreshWindowAndDocument();
            _hasWindow = _windowAndDocument != null;

            if (_hasWindow)
            {
                DomEventJsInterop.AddEventListener<Window>("window", "resize", OnResize, false);
            }
        }
    }

    private async Task<WindowAndDocument> RefreshWindowAndDocument()
    {
        string[] windowProps = { "innerHeight", "innerWidth", "pageXOffset", "pageYOffset" };
        string[] documentProps = { "clientHeight", "clientWidth", "scrollLeft", "scrollTop" };

        _windowAndDocument = await JsInvokeAsync<WindowAndDocument>(JsInteropConstants.GetWindowAndDocumentProps,
            windowProps, documentProps);
        return _windowAndDocument;
    }

    private void OnResize(Window window)
    {
        if (!IsActive) return;
        _ = InvokeAsync(() => UpdateDimensions());
    }

    protected virtual async Task ShowLazyContent()
    {
        await AfterShowContent();
        await MoveContentTo();
        await UpdateDimensions();
    }

    protected virtual Task AfterShowContent()
    {
        return Task.CompletedTask;
    }

    protected abstract Task MoveContentTo();

    protected async Task UpdateDimensions(Action lazySetter = null)
    {
        await RefreshWindowAndDocument();
        await CheckActivatorFixed();
        CheckForPageYOffset();

        PageWidth = _windowAndDocument.ClientWidth;

        if (!HasActivator || Absolute)
        {
            Dimensions.activator = AbsolutePosition();
        }
        else
        {
            var activatorElement = GetActivator();
            var activator = await activatorElement.GetDomInfoAsync();

            Dimensions.activator = await Measure(activatorElement);
            Dimensions.activator.OffsetLeft = activator?.OffsetLeft ?? 0;

            if (Attach != null)
            {
                Dimensions.activator.OffsetTop = activator?.OffsetTop ?? 0;
            }
            else
            {
                Dimensions.activator.OffsetTop = 0;
            }
        }

        var contentElement = Document.GetElementByReference(ContentRef);
        Dimensions.content = await Measure(contentElement);

        lazySetter?.Invoke();
        InternalZIndex = await CalculateZIndex();

        StateHasChanged();
    }

    private async Task CheckActivatorFixed()
    {
        ActivatorFixed = await JsInvokeAsync<bool>(JsInteropConstants.CheckElementFixed, ActivatorSelector);
    }

    private void CheckForPageYOffset()
    {
        if (_hasWindow)
        {
            PageYOffset = ActivatorFixed ? 0 : GetOffsetTop();
        }
    }

    private double GetOffsetTop()
    {
        if (!_hasWindow) return 0;

        return _windowAndDocument.PageYOffset > 0 ? _windowAndDocument.PageYOffset : _windowAndDocument.ScrollTop;
    }

    private Position AbsolutePosition() => new()
    {
        OffsetTop = PositionY ?? _absoluteY,
        OffsetLeft = PositionX ?? _absoluteX,
        ScrollHeight = 0,
        Top = PositionY ?? _absoluteY,
        Bottom = PositionY ?? _absoluteY,
        Left = PositionX ?? _absoluteX,
        Right = PositionX ?? _absoluteX,
        Height = 0,
        Width = 0
    };

    private async Task<Position> Measure(HtmlElement element)
    {
        if (element == null || !_hasWindow) return null;

        var originRect = await element.GetBoundingClientRectAsync();

        var rect = new Position(originRect);

        if (Attach != null)
        {
            var marginLeft = "margin-left";
            var marginRight = "margin-right";

            var styles = await element.GetStylesAsync(marginLeft, marginRight);

            // TODO: check parse "2px"

            if (int.TryParse(styles[marginLeft], out var left))
            {
                rect!.Left = left;
            }

            if (int.TryParse(styles[marginRight], out var right))
            {
                rect!.Right = right;
            }
        }

        return rect;
    }

    protected double CalcXOverflow(double left, double menuWidth)
    {
        var xOverflow = left + menuWidth - PageWidth + 12;

        if ((!Left || Right) && xOverflow > 0)
        {
            left = Math.Max(left - xOverflow, 0);
        }
        else
        {
            left = Math.Max(left, 12);
        }

        return left + GetOffsetLeft();
    }

    private double GetOffsetLeft()
    {
        if (!_hasWindow) return 0;

        return _windowAndDocument.PageXOffset > 0 ? _windowAndDocument.PageXOffset : _windowAndDocument.ScrollLeft;
    }

    protected double CalcYOverflow(double top)
    {
        if (!_hasWindow) return 0;

        var documentHeight = GetInnerHeight();
        var toTop = PageYOffset + documentHeight;
        var activator = Dimensions.activator;
        var contentHeight = Dimensions.content.Height;
        var totalHeight = top + contentHeight;
        var isOverflowing = toTop < totalHeight;

        if (isOverflowing && OffsetOverflow && activator.Top > contentHeight)
        {
            top = PageYOffset + (activator.Top - contentHeight);
        }
        else if (isOverflowing && !AllowOverflow)
        {
            top = toTop - contentHeight - 12;
        }
        else if (top < PageYOffset && !AllowOverflow)
        {
            top = PageYOffset + 12;
        }

        return top < 12 ? 12 : top;
    }

    private double GetInnerHeight()
    {
        if (!_hasWindow) return 0;

        return _windowAndDocument.InnerHeight > 0 ? _windowAndDocument.InnerHeight : _windowAndDocument.ClientHeight;
    }

    //Active
    private async Task CallActivate(Action lazySetter)
    {
        if (!_hasWindow) return;

        await Activate(lazySetter);
    }

    protected virtual Task Activate(Action lazySetter)
    {
        lazySetter();

        return Task.CompletedTask;
    }

    //Deactivate
    private Task CallDeactivate(Action lazySetter)
    {
        return Deactivate(lazySetter);
    }

    protected virtual Task Deactivate(Action lazySetter)
    {
        lazySetter();
        return Task.CompletedTask;
    }

    protected override Dictionary<string, (EventCallback<MouseEventArgs> listener, EventListenerActions actions)> GenActivatorMouseListeners()
    {
        var listeners = base.GenActivatorMouseListeners();

        if (listeners.ContainsKey("onexclick"))
        {
            var onClick = listeners["onexclick"].listener;
            var actions = listeners["onexclick"].actions;

            listeners["onexclick"] = (CreateEventCallback<MouseEventArgs>(async e =>
            {
                if (OpenOnClick && onClick.HasDelegate)
                {
                    await onClick.InvokeAsync(e);
                }

                _absoluteX = e.ClientX;
                _absoluteY = e.ClientY;

                //REVIEW:
                await UpdateDimensions();
            }), actions);
        }

        if (listeners.ContainsKey("onexmouseleave"))
        {
            var cb = listeners["onexmouseleave"].listener;
            var actions = listeners["onexmouseleave"].actions;

            // ContentRef is null if use the feature ShowLazyContent
            if (ContentRef.Context != null)
            {
                if (actions == null)
                {
                    actions = new EventListenerActions(Document.GetElementByReference(ContentRef).Selector);
                }
                else
                {
                    actions.RelatedTarget = Document.GetElementByReference(ContentRef).Selector;
                }
            }

            listeners["onexmouseleave"] = (CreateEventCallback<MouseEventArgs>(async e =>
            {
                if (cb.HasDelegate)
                {
                    await cb.InvokeAsync(e);
                }
            }), actions);
        }

        return listeners;
    }

    protected override Dictionary<string, EventCallback<FocusEventArgs>> GenActivatorFocusListeners()
    {
        var listeners = base.GenActivatorFocusListeners();

        ResetListener(ref listeners, InternalListenerEvent.Focus);
        ResetListener(ref listeners, InternalListenerEvent.Blur);

        return listeners;
    }

    private void ResetListener<T>(ref Dictionary<string, EventCallback<T>> listeners, InternalListenerEvent @event)
    {
        var type = @event.ToString().ToLower();

        if (!listeners.ContainsKey(type)) return;

        var cb = listeners[type];

        listeners[type] = CreateEventCallback<T>(async e =>
        {
            if (cb.HasDelegate)
            {
                await cb.InvokeAsync(e);
            }
        });
    }

    private async Task<int> CalculateZIndex()
    {
        if (ZIndex != null)
        {
            var (isNumber, number) = ZIndex.TryGetNumber();

            if (isNumber && number > 0)
            {
                return Convert.ToInt32(number);
            }
        }

        return await ActiveZIndex();
    }

    private async Task<int> ActiveZIndex()
    {
        return await GetMaxZIndex() + 2;
    }

    private async Task<int> GetMaxZIndex()
    {
        var maxZindex = await JsInvokeAsync<int>(JsInteropConstants.GetMenuOrDialogMaxZIndex, new List<ElementReference> { ContentRef }, Ref);

        return maxZindex > _stackMinZIndex ? maxZindex : _stackMinZIndex;
    }

    public async ValueTask DisposeAsync()
    {
        if (_hasWindow)
        {
            DomEventJsInterop.RemoveEventListener<Window>("window", "resize", OnResize);
        }

        if (ContentRef.Context != null)
        {
            object selectors = new[]
            {
                GetContent().Selector,
                ActivatorSelector
            };


            _ = JsInvokeAsync(JsInteropConstants.RemoveOutsideClickEventListener, selectors);
        }

        await DeleteContent();
    }

    private HtmlElement GetContent() => Document.GetElementByReference(ContentRef);

    private async Task DeleteContent()
    {
        try
        {
            if (ContentRef.Context != null)
            {
                await JsInvokeAsync(JsInteropConstants.DelElementFrom, ContentRef, AttachedSelector);
            }
        }
        catch (Exception e)
        {
            // ignored
        }
    }

    protected class Position : BoundingClientRect
    {
        public double OffsetTop { get; set; }
        public double OffsetLeft { get; set; }
        public double ScrollHeight { get; set; }

        public Position()
        {
        }

        public Position(BoundingClientRect rect)
        {
            Bottom = rect?.Bottom ?? 0;
            Left = rect?.Left ?? 0;
            Height = rect?.Height ?? 0;
            Right = rect?.Right ?? 0;
            Top = rect?.Top ?? 0;
            Width = rect?.Width ?? 0;
            X = rect?.X ?? 0;
            Y = rect?.Y ?? 0;
        }
    }

    private enum InternalListenerEvent
    {
        None,
        Mouseenter,
        Click,
        Focus,
        Blur
    }
}