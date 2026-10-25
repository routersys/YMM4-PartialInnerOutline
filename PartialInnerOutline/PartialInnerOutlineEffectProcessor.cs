using Vortice;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using YukkuriMovieMaker.Plugin.Brush;
using AlphaMask = Vortice.Direct2D1.Effects.AlphaMask;
using Blend = Vortice.Direct2D1.Effects.Blend;
using ProjectBlend = YukkuriMovieMaker.Project.Blend;

namespace PartialInnerOutline;

internal sealed class PartialInnerOutlineEffectProcessor : VideoEffectProcessorBase
{
    private readonly IGraphicsDevicesAndContext _devices;
    private readonly PartialInnerOutlineEffect _item;
    private readonly List<OutlineDilationCustomEffect> _dilations = [];
    private readonly float[] _steps = new float[PartialInnerOutlineSettings.MaximumStepCount];
    private readonly int[] _inflations = new int[PartialInnerOutlineSettings.MaximumStepCount];
    private OutlineMaskCustomEffect? _mask;
    private AlphaMask? _brushMask;
    private GaussianBlur? _blur;
    private AlphaMask? _clip;
    private Composite? _composite;
    private Blend? _blend;
    private Opacity? _empty;
    private AffineTransform2D? _sink;
    private readonly IBrushSource _brushSource;
    private ID2D1CommandList? _brushCommandList;
    private bool _isBrushStale = true;
    private RawRectF? _fillBounds;
    private OutputKind? _outputKind;
    private ProjectBlend _blendMode;
    private PlanKey? _plan;
    private int _passCount;
    private int? _padding;
    private double? _blurValue;
    private PartialInnerOutlineSettings.FeatherRamp? _ramp;
    private float? _opacity;
    private PartialInnerOutlineSettings.BandGeometry? _band;

    public PartialInnerOutlineEffectProcessor(IGraphicsDevicesAndContext devices, PartialInnerOutlineEffect item)
        : base(devices)
    {
        _devices = devices;
        _item = item;
        _brushSource = item.Brush.CreateBrush(devices);
        disposer.Collect(_brushSource);
    }

    public override DrawDescription Update(EffectDescription effectDescription)
    {
        try
        {
            return UpdateCore(effectDescription);
        }
        catch (Exception exception)
        {
            PartialInnerOutlineTelemetry.Report(exception);
            throw;
        }
    }

    private DrawDescription UpdateCore(EffectDescription effectDescription)
    {
        if (IsPassThroughEffect || _mask is null || _blur is null || _sink is null || input is null)
            return effectDescription.DrawDescription;

        var frame = effectDescription.ItemPosition.Frame;
        var length = effectDescription.ItemDuration.Frame;
        var fps = effectDescription.FPS;
        var thickness = Math.Clamp(Math.Round(_item.Thickness.GetValue(frame, length, fps), PartialInnerOutlineSettings.ThicknessDecimals), 0.0, PartialInnerOutlineSettings.MaximumThickness);
        var quality = (int)Math.Clamp(_item.Quality.GetValue(frame, length, fps), PartialInnerOutlineSettings.MinimumQuality, PartialInnerOutlineSettings.MaximumQuality);
        var smoothness = _item.Smoothness.GetValue(frame, length, fps) / PartialInnerOutlineSettings.PercentScale;
        var width = _item.BandWidth.GetValue(frame, length, fps);
        var opacity = Math.Clamp(_item.Opacity.GetValue(frame, length, fps) / PartialInnerOutlineSettings.PercentScale, 0.0, 1.0);
        var blur = Math.Clamp(_item.Blur.GetValue(frame, length, fps), 0.0, PartialInnerOutlineSettings.MaximumBlur);
        var isAngular = _item.IsAngular;
        var isOutlineOnly = _item.IsOutlineOnly;
        var blendMode = _item.Blend;

        var bounds = _devices.DeviceContext.GetImageLocalBounds(input);
        var hasOutline = IsUsable(bounds) && thickness > 0.0 && width > 0.0 && opacity > 0.0;

        var outputKind = !hasOutline ? (isOutlineOnly ? OutputKind.Empty : OutputKind.Input)
            : isOutlineOnly ? OutputKind.Outline
            : blendMode.IsCompositionEffect() ? OutputKind.Composite
            : OutputKind.Blend;
        if (_outputKind != outputKind || (outputKind >= OutputKind.Composite && _blendMode != blendMode))
            ConnectSink(outputKind, blendMode);

        if (!hasOutline)
            return effectDescription.DrawDescription;

        var blurPadding = PartialInnerOutlineSettings.GetBlurPadding(blur);
        var plan = new PlanKey(thickness, quality, isAngular);
        if (_plan != plan)
            ApplyPlan(plan);

        if (_padding != blurPadding)
        {
            _mask.OutputInflation = blurPadding;
            _padding = blurPadding;
        }

        if (_blurValue != blur)
        {
            _blur.StandardDeviation = (float)blur;
            _blurValue = blur;
        }

        var ramp = PartialInnerOutlineSettings.GetFeatherRamp(smoothness);
        if (_ramp != ramp)
        {
            _mask.FeatherLow = ramp.Low;
            _mask.FeatherInverseWidth = ramp.InverseWidth;
            _ramp = ramp;
        }

        var opacityKey = (float)opacity;
        if (_opacity != opacityKey)
        {
            _mask.Opacity = opacityKey;
            _opacity = opacityKey;
        }

        var band = PartialInnerOutlineSettings.GetBand(
            _item.BandDirection.GetValue(frame, length, fps),
            _item.BandPosition.GetValue(frame, length, fps),
            width,
            _item.BandSoftness.GetValue(frame, length, fps),
            bounds.Left,
            bounds.Top,
            bounds.Right,
            bounds.Bottom);
        if (_band != band)
        {
            _mask.BandDirectionX = band.DirectionX;
            _mask.BandDirectionY = band.DirectionY;
            _mask.BandOffset = band.Offset;
            _mask.BandEdgeNear = band.EdgeNear;
            _mask.BandEdgeFar = band.EdgeFar;
            _band = band;
        }

        UpdateBrush(effectDescription, new RawRectF(bounds.Left - blurPadding, bounds.Top - blurPadding, bounds.Right + blurPadding, bounds.Bottom + blurPadding));
        return effectDescription.DrawDescription;
    }

    private static bool IsUsable(RawRectF bounds)
        => float.IsFinite(bounds.Left) && float.IsFinite(bounds.Top) && float.IsFinite(bounds.Right) && float.IsFinite(bounds.Bottom)
            && bounds.Right > bounds.Left && bounds.Bottom > bounds.Top;

    private void UpdateBrush(EffectDescription effectDescription, RawRectF fillBounds)
    {
        if (_brushMask is null)
            return;

        var isChanged = _brushSource.Update(new BrushSourceDescription(effectDescription, fillBounds));
        var isSameBounds = _fillBounds is { } previous && previous.Equals(fillBounds);
        if (!_isBrushStale && !isChanged && isSameBounds)
            return;

        disposer.RemoveAndDispose(ref _brushCommandList);
        var commandList = _devices.DeviceContext.CreateCommandList();
        disposer.Collect(commandList);
        _brushCommandList = commandList;

        var deviceContext = _devices.DeviceContext;
        deviceContext.Target = commandList;
        try
        {
            deviceContext.BeginDraw();
            deviceContext.Clear(null);
            deviceContext.FillRectangle(fillBounds, _brushSource.Brush);
            deviceContext.EndDraw();
        }
        finally
        {
            deviceContext.Target = null;
        }

        commandList.Close();
        _brushMask.SetInput(0, commandList, true);
        _fillBounds = fillBounds;
        _isBrushStale = false;
    }

    private void ApplyPlan(PlanKey plan)
    {
        var count = PartialInnerOutlineSettings.DecomposeThickness(plan.Thickness, _steps);
        PartialInnerOutlineSettings.GetOutputInflations(_steps.AsSpan(0, count), _inflations);
        EnsureDilations(count);
        for (var index = 0; index < count; index++)
        {
            var step = _steps[index];
            var dilation = _dilations[index];
            dilation.StepPixels = step;
            dilation.SampleCount = PartialInnerOutlineSettings.GetRingSampleCount(step, plan.IsAngular, plan.Quality);
            dilation.IsAngular = plan.IsAngular;
            dilation.OutputInflation = _inflations[index];
            dilation.Cached = index == count - 1;
        }

        ConnectChain(count);
        _passCount = count;
        _plan = plan;
    }

    private void EnsureDilations(int count)
    {
        while (_dilations.Count < count)
        {
            var dilation = new OutlineDilationCustomEffect(_devices);
            if (!dilation.IsEnabled)
            {
                dilation.Dispose();
                throw new InvalidOperationException("The outline dilation effect could not be created.");
            }

            dilation.IsInverted = _dilations.Count == 0;
            _dilations.Add(dilation);
            disposer.Collect(dilation);
        }
    }

    private void ConnectChain(int count)
    {
        for (var index = 0; index < _dilations.Count; index++)
        {
            if (index >= count)
                _dilations[index].SetInput(0, null, true);
            else if (index == 0)
                _dilations[0].SetInput(0, input, true);
            else
            {
                using var previous = _dilations[index - 1].Output;
                _dilations[index].SetInput(0, previous, true);
            }
        }

        if (count > 0)
        {
            using var last = _dilations[count - 1].Output;
            _mask?.SetInput(0, last, true);
        }
        else
            _mask?.SetInput(0, null, true);
    }

    private void ConnectSink(OutputKind kind, ProjectBlend blendMode)
    {
        if (_sink is null || _empty is null || _clip is null || _composite is null || _blend is null)
            return;

        switch (kind)
        {
            case OutputKind.Input:
                _sink.SetInput(0, input, true);
                break;
            case OutputKind.Empty:
                {
                    using var output = _empty.Output;
                    _sink.SetInput(0, output, true);
                    break;
                }
            case OutputKind.Outline:
                {
                    using var output = _clip.Output;
                    _sink.SetInput(0, output, true);
                    break;
                }
            case OutputKind.Composite:
                {
                    _composite.Mode = blendMode.ToD2DCompositionMode();
                    using var output = _composite.Output;
                    _sink.SetInput(0, output, true);
                    break;
                }
            default:
                {
                    _blend.Mode = blendMode.ToD2DBlendMode();
                    using var output = _blend.Output;
                    _sink.SetInput(0, output, true);
                    break;
                }
        }

        _outputKind = kind;
        _blendMode = blendMode;
    }

    protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
    {
        var mask = new OutlineMaskCustomEffect(devices);
        if (!mask.IsEnabled)
        {
            mask.Dispose();
            return null;
        }

        _mask = mask;
        disposer.Collect(mask);

        _brushMask = new AlphaMask(devices.DeviceContext);
        disposer.Collect(_brushMask);
        using (var maskOutput = mask.Output)
            _brushMask.SetInput(1, maskOutput, true);

        _blur = new GaussianBlur(devices.DeviceContext);
        disposer.Collect(_blur);
        using (var maskedOutput = _brushMask.Output)
            _blur.SetInput(0, maskedOutput, true);

        _clip = new AlphaMask(devices.DeviceContext);
        disposer.Collect(_clip);
        using (var blurredOutput = _blur.Output)
            _clip.SetInput(0, blurredOutput, true);

        _composite = new Composite(devices.DeviceContext);
        disposer.Collect(_composite);
        _blend = new Blend(devices.DeviceContext);
        disposer.Collect(_blend);
        using (var clippedOutput = _clip.Output)
        {
            _composite.SetInput(1, clippedOutput, true);
            _blend.SetInput(1, clippedOutput, true);
        }

        _empty = new Opacity(devices.DeviceContext) { Value = 0f };
        disposer.Collect(_empty);

        _sink = new AffineTransform2D(devices.DeviceContext);
        disposer.Collect(_sink);
        var result = _sink.Output;
        disposer.Collect(result);
        return result;
    }

    protected override void setInput(ID2D1Image? input)
    {
        try
        {
            SetInputCore(input);
        }
        catch (Exception exception)
        {
            PartialInnerOutlineTelemetry.Report(exception);
            throw;
        }
    }

    private void SetInputCore(ID2D1Image? image)
    {
        if (_passCount > 0)
            _dilations[0].SetInput(0, image, true);
        _clip?.SetInput(1, image, true);
        _composite?.SetInput(0, image, true);
        _blend?.SetInput(0, image, true);
        _empty?.SetInput(0, image, true);
        if (_outputKind == OutputKind.Input)
            _sink?.SetInput(0, image, true);
    }

    protected override void ClearEffectChain()
    {
        try
        {
            foreach (var dilation in _dilations)
                dilation.SetInput(0, null, true);
            _mask?.SetInput(0, null, true);
            _brushMask?.SetInput(0, null, true);
            _brushMask?.SetInput(1, null, true);
            _blur?.SetInput(0, null, true);
            _clip?.SetInput(0, null, true);
            _clip?.SetInput(1, null, true);
            _composite?.SetInput(0, null, true);
            _composite?.SetInput(1, null, true);
            _blend?.SetInput(0, null, true);
            _blend?.SetInput(1, null, true);
            _empty?.SetInput(0, null, true);
            _sink?.SetInput(0, null, true);
        }
        catch (Exception exception)
        {
            PartialInnerOutlineTelemetry.Report(exception);
            throw;
        }
    }

    private enum OutputKind
    {
        Input,
        Empty,
        Outline,
        Composite,
        Blend,
    }

    private readonly record struct PlanKey(double Thickness, int Quality, bool IsAngular);
}
