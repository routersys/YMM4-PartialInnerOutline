using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace PartialInnerOutline;

internal sealed class OutlineMaskCustomEffect(IGraphicsDevicesAndContext devices)
    : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
{
    internal enum PropertyIndex
    {
        FeatherLow = 0,
        FeatherInverseWidth,
        Opacity,
        BandOffset,
        BandDirectionX,
        BandDirectionY,
        BandEdgeNear,
        BandEdgeFar,
        OutputInflation,
    }

    public float FeatherLow { set => SetValue((int)PropertyIndex.FeatherLow, value); }
    public float FeatherInverseWidth { set => SetValue((int)PropertyIndex.FeatherInverseWidth, value); }
    public float Opacity { set => SetValue((int)PropertyIndex.Opacity, value); }
    public float BandOffset { set => SetValue((int)PropertyIndex.BandOffset, value); }
    public float BandDirectionX { set => SetValue((int)PropertyIndex.BandDirectionX, value); }
    public float BandDirectionY { set => SetValue((int)PropertyIndex.BandDirectionY, value); }
    public float BandEdgeNear { set => SetValue((int)PropertyIndex.BandEdgeNear, value); }
    public float BandEdgeFar { set => SetValue((int)PropertyIndex.BandEdgeFar, value); }
    public int OutputInflation { set => SetValue((int)PropertyIndex.OutputInflation, value); }

    [CustomEffect(1)]
    internal sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
    {
        private ConstantBuffer _cb;
        private int _outputInflation;

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.FeatherLow)]
        public float FeatherLow { get => _cb.FeatherLow; set { _cb.FeatherLow = Sanitize(value, 0f, 1f); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.FeatherInverseWidth)]
        public float FeatherInverseWidth { get => _cb.FeatherInverseWidth; set { _cb.FeatherInverseWidth = Sanitize(value, 0f, float.MaxValue); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Opacity)]
        public float Opacity { get => _cb.Opacity; set { _cb.Opacity = Sanitize(value, 0f, 1f); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.BandOffset)]
        public float BandOffset { get => _cb.BandOffset; set { _cb.BandOffset = Sanitize(value, float.MinValue, float.MaxValue); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.BandDirectionX)]
        public float BandDirectionX { get => _cb.BandDirectionX; set { _cb.BandDirectionX = Sanitize(value, -1f, 1f); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.BandDirectionY)]
        public float BandDirectionY { get => _cb.BandDirectionY; set { _cb.BandDirectionY = Sanitize(value, -1f, 1f); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.BandEdgeNear)]
        public float BandEdgeNear { get => _cb.BandEdgeNear; set { _cb.BandEdgeNear = Sanitize(value, 0f, float.MaxValue); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.BandEdgeFar)]
        public float BandEdgeFar { get => _cb.BandEdgeFar; set { _cb.BandEdgeFar = Sanitize(value, 0f, float.MaxValue); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Int32, (int)PropertyIndex.OutputInflation)]
        public int OutputInflation { get => _outputInflation; set => _outputInflation = Math.Clamp(value, 0, PartialInnerOutlineSettings.MaximumBlurPadding); }

        public EffectImpl() : base(ShaderResourceUri.Get("OutlineMask"))
        {
            _cb.FeatherLow = (float)PartialInnerOutlineSettings.CoverageThreshold;
            _cb.Opacity = 1f;
        }

        private static float Sanitize(float value, float minimum, float maximum)
            => float.IsNaN(value) ? 0f : Math.Clamp(value, minimum, maximum);

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
            _cb.SourceBounds = new Vector4(inputRect.Left, inputRect.Top, inputRect.Right, inputRect.Bottom);
            UpdateConstants();
        }

        public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            => inputRects[0] = outputRect;

        [StructLayout(LayoutKind.Sequential)]
        private struct ConstantBuffer
        {
            public float FeatherLow;
            public float FeatherInverseWidth;
            public float Opacity;
            public float BandOffset;
            public float BandDirectionX;
            public float BandDirectionY;
            public float BandEdgeNear;
            public float BandEdgeFar;
            public Vector4 SourceBounds;
        }
    }
}
