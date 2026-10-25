using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace PartialInnerOutline;

internal sealed class OutlineDilationCustomEffect(IGraphicsDevicesAndContext devices)
    : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
{
    internal enum PropertyIndex
    {
        StepPixels = 0,
        SampleCount,
        IsAngular,
        IsInverted,
        OutputInflation,
    }

    public float StepPixels { set => SetValue((int)PropertyIndex.StepPixels, value); }
    public int SampleCount { set => SetValue((int)PropertyIndex.SampleCount, value); }
    public bool IsAngular { set => SetValue((int)PropertyIndex.IsAngular, value); }
    public bool IsInverted { set => SetValue((int)PropertyIndex.IsInverted, value); }
    public int OutputInflation { set => SetValue((int)PropertyIndex.OutputInflation, value); }

    [CustomEffect(1)]
    internal sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
    {
        private ConstantBuffer _cb;
        private int _outputInflation;

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.StepPixels)]
        public float StepPixels { get => _cb.StepPixels; set { _cb.StepPixels = float.IsNaN(value) ? 0f : Math.Clamp(value, 0f, (float)PartialInnerOutlineSettings.MaximumThickness); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Int32, (int)PropertyIndex.SampleCount)]
        public int SampleCount
        {
            get => _cb.SampleCount;
            set
            {
                _cb.SampleCount = Math.Clamp(value, 0, PartialInnerOutlineSettings.MaximumQuality);
                var turn = _cb.SampleCount > 0 ? 2.0 * Math.PI / _cb.SampleCount : 0.0;
                _cb.RotationCos = (float)Math.Cos(turn);
                _cb.RotationSin = (float)Math.Sin(turn);
                UpdateConstants();
            }
        }

        [CustomEffectProperty(PropertyType.Bool, (int)PropertyIndex.IsAngular)]
        public bool IsAngular { get => _cb.IsAngular != 0; set { _cb.IsAngular = value ? 1 : 0; UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Bool, (int)PropertyIndex.IsInverted)]
        public bool IsInverted { get => _cb.IsInverted != 0; set { _cb.IsInverted = value ? 1 : 0; UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Int32, (int)PropertyIndex.OutputInflation)]
        public int OutputInflation { get => _outputInflation; set => _outputInflation = Math.Clamp(value, -PartialInnerOutlineSettings.MaximumInflation, PartialInnerOutlineSettings.MaximumInflation); }

        public EffectImpl() : base(ShaderResourceUri.Get("OutlineDilation"))
        {
            _cb.RotationCos = 1f;
        }

        protected override void UpdateConstants()
        {
            if (drawInformation is null)
                return;

            try
            {
                drawInformation.SetPixelShaderConstantBuffer(_cb);
            }
            catch (Exception exception)
            {
                PartialInnerOutlineTelemetry.Report(exception);
                throw;
            }
        }

        public override void MapInputRectsToOutputRect(
            RawRect[] inputRects,
            RawRect[] inputOpaqueSubRects,
            out RawRect outputRect,
            out RawRect outputOpaqueSubRect)
        {
            inputRect = ClampInputRect(inputRects[0]);
            outputRect = inputRect.Inflate(_outputInflation);
            outputOpaqueSubRect = default;
        }

        public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            => inputRects[0] = outputRect.Inflate(PartialInnerOutlineSettings.GetReach(_cb.StepPixels));

        public override RawRect MapInvalidRect(int inputIndex, RawRect invalidInputRect)
            => invalidInputRect.Inflate(PartialInnerOutlineSettings.GetReach(_cb.StepPixels));

        [StructLayout(LayoutKind.Sequential)]
        private struct ConstantBuffer
        {
            public float StepPixels;
            public int SampleCount;
            public int IsAngular;
            public int IsInverted;
            public float RotationCos;
            public float RotationSin;
            public float Pad0;
            public float Pad1;
        }
    }
}
