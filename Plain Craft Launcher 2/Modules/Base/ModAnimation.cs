using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
// 动画引擎模块
// 使用 Ani 作为方法或属性的开头，使用 Aa 作为单个动画对象的开头（便于自动补全）

using System.Windows.Media.Effects;
using MeloongCore;
using MeloongCore.Wpf.Extensions;
using Newtonsoft.Json.Linq;

namespace PCL
{

    public static partial class ModAnimation
    {

        #region 声明

        /// <summary>
    /// 动画速度。最大为 200。
    /// </summary>
        public static double AniSpeed
        {
            get
            {
                int Setting = Settings.Get<int>("SystemDebugAnim");
                return Setting >= 30 ? 200d : (Setting * 0.1d + 0.1d).Clamp(0.1d, 3d);
            }
        }
        /// <summary>
    /// 动画组列表。
    /// </summary>
        public static Dictionary<string, AniGroupEntry> AniGroups = new Dictionary<string, AniGroupEntry>();
        public class AniGroupEntry
        {
            public List<AniData> Data;
            public long StartTick;
            public int Uuid = ModBase.GetUuid();
        }
        /// <summary>
    /// 上一次记刻的时间。
    /// </summary>
        private static long AniLastTick;
        /// <summary>
    /// 动画模块是否正在运行。
    /// </summary>
        public static bool AniRunning = false;
        private static int _AniControlEnabled = 0;
        private readonly static object AniControlEnabledLock = new object();
        /// <summary>
    /// 控件动画执行是否开启。先 +1，再 -1。
    /// </summary>
        public static int AniControlEnabled
        {
            get
            {
                return _AniControlEnabled;
            }
            set
            {
                lock (AniControlEnabledLock)
                    _AniControlEnabled = value;
            }
        }

        #endregion

        #region 类与枚举

        /// <summary>
    /// 单个动画对象。
    /// </summary>
    /// <remarks></remarks>
        public struct AniData
        {

            /// <summary>
        /// 动画种类。
        /// </summary>
        /// <remarks></remarks>
            public AniType TypeMain;
            /// <summary>
        /// 动画副种类。
        /// </summary>
        /// <remarks></remarks>
            public AniTypeSub TypeSub;

            /// <summary>
        /// 动画总长度。
        /// </summary>
        /// <remarks></remarks>
            public int TimeTotal;
            /// <summary>
        /// 已经执行的动画长度。如果为负数则为延迟。
        /// </summary>
        /// <remarks></remarks>
            public double TimeFinished;
            /// <summary>
        /// 已经完成的百分比。
        /// </summary>
        /// <remarks></remarks>
            public double TimePercent;

            /// <summary>
        /// 是否为“以后”。
        /// </summary>
        /// <remarks></remarks>
            public bool IsAfter;

            /// <summary>
        /// 插值器类型。
        /// </summary>
        /// <remarks></remarks>
            public AniEase Ease;
            /// <summary>
        /// 动画对象。
        /// </summary>
        /// <remarks></remarks>
            public object Obj;
            /// <summary>
        /// 动画值。
        /// </summary>
        /// <remarks></remarks>
            public object Value;
            /// <summary>
        /// 上次执行时的动画值。
        /// </summary>
        /// <remarks></remarks>
            public object ValueLast;

            public override string ToString()
            {
                return TypeMain.ToString() + " | " + TimeFinished + "/" + TimeTotal + "(" + Math.Round(TimePercent * 100d) + "%)" + (Obj is null ? "" : " | " + Obj.ToString() + "(" + Obj.GetType().Name + ")");
            }

        }

        /// <summary>
    /// 动画基础种类。
    /// </summary>
        public enum AniType
        {
            /// <summary>
        /// 单个Double的动画，包括位置、长宽、透明度等。这需要附属类型。
        /// </summary>
        /// <remarks></remarks>
            Number,
            /// <summary>
        /// 颜色属性的动画。这需要附属类型。
        /// </summary>
        /// <remarks></remarks>
            Color,
            /// <summary>
        /// 缩放控件大小。比起4个DoubleAnimation来说效率更高。
        /// </summary>
        /// <remarks></remarks>
            Scale,
            /// <summary>
        /// 文字一个个出现。
        /// </summary>
        /// <remarks></remarks>
            TextAppear,
            /// <summary>
        /// 执行代码。
        /// </summary>
        /// <remarks></remarks>
            Code,
            /// <summary>
        /// 以 WPF 方式缩放控件。
        /// </summary>
            ScaleTransform,
            /// <summary>
        /// 以 WPF 方式旋转控件。
        /// </summary>
            RotateTransform
        }
        /// <summary>
    /// 动画扩展种类。
    /// </summary>
        public enum AniTypeSub
        {
            X,
            Y,
            Width,
            Height,
            Opacity,
            Value,
            Radius,
            BorderThickness,
            StrokeThickness,
            TranslateX,
            TranslateY,
            Double,
            DoubleParam,
            GridLengthWidth
        }

        #endregion

        #region 种类

        // DoubleAnimation

        /// <summary>
    /// 移动X轴的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Value">进行移动的值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
        public static AniData AaX(object Obj, double Value, int Time = 400, int Delay = 0, AniEase Ease = null, bool After = false)
        {
            return new AniData()
            {
                TypeMain = AniType.Number,
                TypeSub = AniTypeSub.X,
                TimeTotal = Time,
                Ease = Ease ?? new AniEaseLinear(),
                Obj = Obj,
                Value = Value,
                IsAfter = After,
                TimeFinished = -Delay
            };
        }
        /// <summary>
    /// 移动Y轴的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Value">进行移动的值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
        public static AniData AaY(object Obj, double Value, int Time = 400, int Delay = 0, AniEase Ease = null, bool After = false)
        {
            return new AniData()
            {
                TypeMain = AniType.Number,
                TypeSub = AniTypeSub.Y,
                TimeTotal = Time,
                Ease = Ease ?? new AniEaseLinear(),
                Obj = Obj,
                Value = Value,
                IsAfter = After,
                TimeFinished = -Delay
            };
        }
        /// <summary>
    /// 改变宽度的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Value">宽度改变的值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
        public static AniData AaWidth(object Obj, double Value, int Time = 400, int Delay = 0, AniEase Ease = null, bool After = false)
        {
            return new AniData()
            {
                TypeMain = AniType.Number,
                TypeSub = AniTypeSub.Width,
                TimeTotal = Time,
                Ease = Ease ?? new AniEaseLinear(),
                Obj = Obj,
                Value = Value,
                IsAfter = After,
                TimeFinished = -Delay
            };
        }
        /// <summary>
    /// 改变高度的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Value">高度改变的值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
        public static AniData AaHeight(object Obj, double Value, int Time = 400, int Delay = 0, AniEase Ease = null, bool After = false)
        {
            return new AniData()
            {
                TypeMain = AniType.Number,
                TypeSub = AniTypeSub.Height,
                TimeTotal = Time,
                Ease = Ease ?? new AniEaseLinear(),
                Obj = Obj,
                Value = Value,
                IsAfter = After,
                TimeFinished = -Delay
            };
        }
        /// <summary>
    /// 改变透明度的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Value">透明度改变的值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
        public static AniData AaOpacity(object Obj, double Value, int Time = 400, int Delay = 0, AniEase Ease = null, bool After = false)
        {
            return new AniData()
            {
                TypeMain = AniType.Number,
                TypeSub = AniTypeSub.Opacity,
                TimeTotal = Time,
                Ease = Ease ?? new AniEaseLinear(),
                Obj = Obj,
                Value = Value,
                IsAfter = After,
                TimeFinished = -Delay
            };
        }
        /// <summary>
    /// 改变 Width 的 GridLength 属性的动画。必须为 Star。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Value">GridLength.Value 改变的值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
        public static AniData AaGridLengthWidth(object Obj, double Value, int Time = 400, int Delay = 0, AniEase Ease = null, bool After = false)
        {
            return new AniData()
            {
                TypeMain = AniType.Number,
                TypeSub = AniTypeSub.GridLengthWidth,
                TimeTotal = Time,
                Ease = Ease ?? new AniEaseLinear(),
                Obj = Obj,
                Value = Value,
                IsAfter = After,
                TimeFinished = -Delay
            };
        }

        // DoubleAnimation（Obj, Prop, [Res]）

        /// <summary>
    /// 改变数字属性的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Prop">动画的依赖属性。</param>
    /// <param name="Value">改变的值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
        public static AniData AaDouble(object Obj, DependencyProperty Prop, double Value, int Time = 400, int Delay = 0, AniEase Ease = null, bool After = false)
        {
            return new AniData() { TypeMain = AniType.Number, TypeSub = AniTypeSub.Double, TimeTotal = Time, Ease = Ease ?? new AniEaseLinear(), Obj = new object[] { Obj, Prop, "" }, Value = Value, IsAfter = After, TimeFinished = -Delay };
        }
        /// <summary>
    /// 获取数字动画值。
    /// </summary>
    /// <param name="Value">改变的值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
        public static AniData AaDouble(ParameterizedThreadStart Lambda, double Value, int Time = 400, int Delay = 0, AniEase Ease = null, bool After = false)
        {
            return new AniData() { TypeMain = AniType.Number, TypeSub = AniTypeSub.DoubleParam, TimeTotal = Time, Ease = Ease ?? new AniEaseLinear(), Obj = Lambda, Value = Value, IsAfter = After, TimeFinished = -Delay };
        }

        // ColorAnimation（Obj, Prop, [Res]）

        /// <summary>
    /// 改变颜色属性的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Prop">动画的依赖属性。</param>
    /// <param name="Value">颜色改变的值。以RGB加减法进行计算。不用担心超额。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
        public static AniData AaColor(FrameworkElement Obj, DependencyProperty Prop, ModBase.MyColor Value, int Time = 400, int Delay = 0, AniEase Ease = null, bool After = false)
        {
            return new AniData() { TypeMain = AniType.Color, TimeTotal = Time, Ease = Ease ?? new AniEaseLinear(), Obj = new object[] { Obj, Prop, "" }, Value = Value, IsAfter = After, TimeFinished = -Delay, ValueLast = new ModBase.MyColor(0d, 0d, 0d, 0d) };
        }
        /// <summary>
    /// 改变颜色属性为一个资源的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Prop">动画的依赖属性。</param>
    /// <param name="Res">要将颜色改变为该资源值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
        public static AniData AaColor(FrameworkElement Obj, DependencyProperty Prop, string Res, int Time = 400, int Delay = 0, AniEase Ease = null, bool After = false)
        {
            return new AniData() { TypeMain = AniType.Color, TimeTotal = Time, Ease = Ease ?? new AniEaseLinear(), Obj = new object[] { Obj, Prop, Res }, Value = new ModBase.MyColor(System.Windows.Application.Current.FindResource(Res)) - new ModBase.MyColor(Obj.GetValue(Prop)), IsAfter = After, TimeFinished = -Delay, ValueLast = new ModBase.MyColor(0d, 0d, 0d, 0d) };
        }

        // Scale

        /// <summary>
    /// 缩放控件的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Value">大小改变的百分比（如-0.6）或值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <param name="Absolute">大小改变是否为绝对值。若为 True 则为绝对像素，若为 False 则为相对百分比。</param>
    /// <returns></returns>
    /// <remarks></remarks>
        public static AniData AaScale(object Obj, double Value, int Time = 400, int Delay = 0, AniEase Ease = null, bool After = false, bool Absolute = false)
        {
            ModBase.MyRect ChangeRect;
            if (Absolute)
            {
                ChangeRect = new ModBase.MyRect(-0.5d * Value, -0.5d * Value, Value, Value);
            }
            else
            {
                var Element = (FrameworkElement)Obj;
                ChangeRect = new ModBase.MyRect(-0.5d * Element.ActualWidth * Value, -0.5d * Element.ActualHeight * Value, Element.ActualWidth * Value, Element.ActualHeight * Value);
            }
            return new AniData() { TypeMain = AniType.Scale, TimeTotal = Time, Ease = Ease ?? new AniEaseLinear(), Obj = Obj, Value = ChangeRect, IsAfter = After, TimeFinished = -Delay };
        }

        // TextAppear

        /// <summary>
    /// 让一段文字一个个字出现或消失的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。必须是Label或TextBlock。</param>
    /// <param name="Hide">是否为一个个字隐藏。默认为False（一个个字出现）。这些字必须已经存在了。</param>
    /// <param name="TimePerText">是否采用根据文本长度决定时间的方式。</param>
    /// <param name="Time">动画长度（毫秒）。若TimePerText为True，这代表每个字所占据的时间。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
        // Code

        /// <summary>
    /// 执行代码。
    /// </summary>
    /// <param name="Code">一个ThreadStart。这将会在执行时在主线程调用。</param>
    /// <param name="Delay">代码延迟执行的时间（毫秒）。</param>
    /// <param name="After">是否等到以前的动画完成后才执行。</param>
    /// <returns></returns>
    /// <remarks></remarks>
        public static AniData AaCode(ThreadStart Code, int Delay = 0, bool After = false)
        {
            return new AniData()
            {
                TypeMain = AniType.Code,
                TimeTotal = 1,
                Value = Code,
                IsAfter = After,
                TimeFinished = -Delay
            };
        }

        // ScaleTransform

        /// <summary>
    /// 按照 WPF 方式缩放控件的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。它必须已经拥有了单一的 ScaleTransform 值。</param>
    /// <param name="Value">大小改变的百分比（如-0.6）。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
        public static AniData AaScaleTransform(object Obj, double Value, int Time = 400, int Delay = 0, AniEase Ease = null, bool After = false)
        {
            return new AniData() { TypeMain = AniType.ScaleTransform, TimeTotal = Time, Ease = Ease ?? new AniEaseLinear(), Obj = Obj, Value = Value, IsAfter = After, TimeFinished = -Delay };
        }

        // RotateTransform

        /// <summary>
    /// 按照 WPF 方式旋转控件的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。它必须已经拥有了单一的 ScaleTransform 值。</param>
    /// <param name="Value">旋转角度（度）。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
        public static AniData AaRotateTransform(object Obj, double Value, int Time = 400, int Delay = 0, AniEase Ease = null, bool After = false)
        {
            return new AniData() { TypeMain = AniType.RotateTransform, TimeTotal = Time, Ease = Ease ?? new AniEaseLinear(), Obj = Obj, Value = Value, IsAfter = After, TimeFinished = -Delay };
        }

        // TranslateTransform

        /// <summary>
    /// 利用 TranslateTransform 移动 X 轴的动画，这不会造成布局更新。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Value">进行移动的值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
        public static AniData AaTranslateX(object Obj, double Value, int Time = 400, int Delay = 0, AniEase Ease = null, bool After = false)
        {
            return new AniData()
            {
                TypeMain = AniType.Number,
                TypeSub = AniTypeSub.TranslateX,
                TimeTotal = Time,
                Ease = Ease ?? new AniEaseLinear(),
                Obj = Obj,
                Value = Value,
                IsAfter = After,
                TimeFinished = -Delay
            };
        }
        /// <summary>
    /// 利用 TranslateTransform 移动 Y 轴的动画，这不会造成布局更新。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Value">进行移动的值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
        public static AniData AaTranslateY(object Obj, double Value, int Time = 400, int Delay = 0, AniEase Ease = null, bool After = false)
        {
            return new AniData()
            {
                TypeMain = AniType.Number,
                TypeSub = AniTypeSub.TranslateY,
                TimeTotal = Time,
                Ease = Ease ?? new AniEaseLinear(),
                Obj = Obj,
                Value = Value,
                IsAfter = After,
                TimeFinished = -Delay
            };
        }

        // 特殊

        #endregion

        #region 缓动函数

        // 基类
        public enum AniEasePower : int
        {
            Weak = 2,
            Middle = 3,
            Strong = 4,
            ExtraStrong = 5
        }
        /// <summary>
    /// 缓动函数基类。
    /// </summary>
        public abstract class AniEase
        {
            /// <summary>
        /// 获取函数值。
        /// </summary>
        /// <param name="t">时间百分比。</param>
            public abstract double GetValue(double t);
            /// <summary>
        /// 获取增量值。
        /// </summary>
        /// <param name="t1">较大的 X。</param>
        /// <param name="t0">较小的 X。</param>
            public virtual double GetDelta(double t1, double t0)
            {
                return GetValue(t1) - GetValue(t0);
            }
        }
        /// <summary>
    /// 渐入渐出组合。
    /// </summary>
        public class AniEaseInout : AniEase
        {
            private readonly AniEase EaseIn;
            private readonly AniEase EaseOut;
            private readonly double EaseInPercent;
            public AniEaseInout(AniEase EaseIn, AniEase EaseOut, double EaseInPercent = 0.5d)
            {
                this.EaseIn = EaseIn;
                this.EaseOut = EaseOut;
                this.EaseInPercent = EaseInPercent;
            }
            public override double GetValue(double t)
            {
                if (t < EaseInPercent)
                {
                    return EaseInPercent * EaseIn.GetValue(t / EaseInPercent);
                }
                else
                {
                    return (1d - EaseInPercent) * EaseOut.GetValue((t - EaseInPercent) / (1d - EaseInPercent)) + EaseInPercent;
                }
            }
        }

        // Linear / 线性
        /// <summary>
    /// 线性，无缓动。
    /// </summary>
        public class AniEaseLinear : AniEase
        {
            public override double GetValue(double t)
            {
                return t.Clamp(0d, 1d);
            }
            public override double GetDelta(double t1, double t0)
            {
                return t1.Clamp(0d, 1d) - t0.Clamp(0d, 1d);
            }
        }

        // Fluent / 平滑
        /// <summary>
    /// 平滑开始。
    /// </summary>
        public class AniEaseInFluent : AniEase
        {
            private readonly AniEasePower p;
            public AniEaseInFluent(AniEasePower Power = AniEasePower.Middle)
            {
                p = Power;
            }
            public override double GetValue(double t)
            {
                return Math.Pow(t.Clamp(0d, 1d), (double)p);
            }
        }
        /// <summary>
    /// 平滑结束。
    /// </summary>
        public class AniEaseOutFluent : AniEase
        {
            private readonly AniEasePower p;
            public AniEaseOutFluent(AniEasePower Power = AniEasePower.Middle)
            {
                p = Power;
            }
            public override double GetValue(double t)
            {
                return 1d - Math.Pow((1d - t).Clamp(0d, 1d), (double)p);
            }
        }
        /// <summary>
    /// 平滑开始与结束。
    /// </summary>
        public class AniEaseInoutFluent : AniEase
        {
            private AniEaseInout Ease;
            public AniEaseInoutFluent(AniEasePower Power = AniEasePower.Middle, double Middle = 0.5d)
            {
                Ease = new AniEaseInout(new AniEaseInFluent(Power), new AniEaseOutFluent(Power), Middle);
            }
            public override double GetValue(double t)
            {
                return Ease.GetValue(t);
            }
        }
        /// <summary>
    /// 以特定速度开始的平滑结束。
    /// </summary>
        public class AniEaseOutFluentWithInitial : AniEase
        {
            private readonly double alpha; // (初速度 / 平均速度) – 1
                                           /// <param name="InitialPixelPerSecond">初速度，px/s</param>
        /// <param name="TotalSecond">总时长，s</param>
        /// <param name="TotalDistance">总路程，px</param>
            public AniEaseOutFluentWithInitial(double InitialPixelPerSecond, double TotalSecond, double TotalDistance)
            {
                double v0_norm = InitialPixelPerSecond * TotalSecond / TotalDistance; // 归一化初速度
                alpha = v0_norm - 1.0d;
                if (alpha < 0d)
                    alpha = 0d; // 初速度小于平均速度时，退化为线性
            }
            public override double GetValue(double percent)
            {
                double p = percent.Clamp(0d, 1d);
                if (alpha == 0d)
                    return p; // 退化到线性
                return (alpha + 1d) * p / (1d + alpha * p);
            }
        }

        // Back / 回弹
        /// <summary>
    /// 回弹开始。有效时间为 1/3。
    /// </summary>
        public class AniEaseInBack : AniEase
        {
            private readonly double p;
            public AniEaseInBack(AniEasePower Power = AniEasePower.Middle)
            {
                p = 3d - (double)Power * 0.5d;
            }
            public override double GetValue(double t)
            {
                t = t.Clamp(0d, 1d);
                return Math.Pow(t, p) * Math.Cos(1.5d * Math.PI * (1d - t));
            }
        }
        /// <summary>
    /// 回弹结束。有效时间为 1/3。
    /// </summary>
        public class AniEaseOutBack : AniEase
        {
            private readonly double p;
            public AniEaseOutBack(AniEasePower Power = AniEasePower.Middle)
            {
                p = 3d - (double)Power * 0.5d;
            }
            public override double GetValue(double t)
            {
                t = t.Clamp(0d, 1d);
                return 1d - Math.Pow(1d - t, p) * Math.Cos(1.5d * Math.PI * t);
            }
        }

        // Car / 平滑-回弹
        /// <summary>
    /// 回弹开始，短平滑结束。
    /// </summary>
        public class AniEaseInCar : AniEase
        {
            private AniEaseInout Ease;
            public AniEaseInCar(double Middle = 0.7d, AniEasePower Power = AniEasePower.Middle)
            {
                Ease = new AniEaseInout(new AniEaseInBack(Power), new AniEaseOutFluent(Power), Middle);
            }
            public override double GetValue(double t)
            {
                return Ease.GetValue(t);
            }
        }
        /// <summary>
    /// 短平滑开始，回弹结束。
    /// </summary>
        public class AniEaseOutCar : AniEase
        {
            private AniEaseInout Ease;
            public AniEaseOutCar(double Middle = 0.3d, AniEasePower Power = AniEasePower.Middle)
            {
                Ease = new AniEaseInout(new AniEaseInFluent(Power), new AniEaseOutBack(Power), Middle);
            }
            public override double GetValue(double t)
            {
                return Ease.GetValue(t);
            }
        }

        // Elastic / 弹簧
        /// <summary>
    /// 弹簧开始。约在 60% 到达最小值。
    /// </summary>
        public class AniEaseInElastic : AniEase
        {
            private readonly int p; // 6~9
            public AniEaseInElastic(AniEasePower Power = AniEasePower.Middle)
            {
                p = (int)Power + 4;
            }
            public override double GetValue(double t)
            {
                t = t.Clamp(0d, 1d);
                return Math.Pow(t, (p - 1) * 0.25d) * Math.Cos((p - 3.5d) * Math.PI * Math.Pow(1d - t, 1.5d));
            }
        }
        /// <summary>
    /// 弹簧结束。约在 40% 到达最大值。
    /// </summary>
        public class AniEaseOutElastic : AniEase
        {
            private readonly int p;
            public AniEaseOutElastic(AniEasePower Power = AniEasePower.Middle)
            {
                p = (int)Power + 4;
            }
            public override double GetValue(double t)
            {
                t = 1d - t.Clamp(0d, 1d);
                return 1d - Math.Pow(t, (p - 1) * 0.25d) * Math.Cos((p - 3.5d) * Math.PI * Math.Pow(1d - t, 1.5d));
            }
        }

        #endregion

        #region 接口（开始、中断、检测）

        /// <summary>
    /// 开始一个动画组。
    /// </summary>
    /// <param name="AniGroup">由 Aa 开头的函数初始化的 AniData 对象集合。</param>
    /// <param name="Name">动画组的名称。如果重复会直接停止同名动画组。</param>
        public static void AniStart(IList AniGroup, string Name = "", bool RefreshTime = false)
        {
            if (RefreshTime)
                AniLastTick = ModBase.GetTimeMs(); // 避免处理动画时已经造成了极大的延迟，导致动画突然结束
                                                   // 添加到正在执行的动画组
            var AllAnis = new List<AniData>();
            foreach (var Element in AniGroup)
            {
                if (Element is ICollection)
                {
                    AllAnis.AddRange((IEnumerable<AniData>)Element);
                }
                else
                {
                    AllAnis.Add((AniData)Element);
                }
            }
            var NewEntry = new AniGroupEntry() { Data = AllAnis, StartTick = ModBase.GetTimeMs() };
            if (string.IsNullOrEmpty(Name))
            {
                Name = NewEntry.Uuid.ToString();
            }
            else
            {
                AniStop(Name);
            }
            AniGroups.Add(Name, NewEntry);
            EnsureAniRendering();
        }
        /// <summary>
    /// 开始一个动画组。
    /// </summary>
        public static void AniStart(AniData AniGroup, string Name = "", bool RefreshTime = false)
        {
            AniStart(new List<AniData>() { AniGroup }, Name, RefreshTime);
        }
        /// <summary>
    /// 直接停止一个动画组。
    /// </summary>
    /// <param name="name">需要停止的动画组的名称。</param>
        public static void AniStop(string Name)
        {
            AniGroups.Remove(Name);
        }
        /// <summary>
    /// 获取动画是否正在进行中。
    /// </summary>
        public static bool AniIsRun(string Name)
        {
            return AniGroups.ContainsKey(Name);
        }

        #endregion

        private static int AniCount = 0;
        private static int AniFPSCounter = 0;
        private static long AniFPSTimer = 0L;
        /// <summary>
    /// 当前的动画 FPS。
    /// </summary>
        public static int AniFPS = 0;

        /// <summary>
        /// 开始动画执行。
        /// 动画帧跟随 WPF 的渲染帧（CompositionTarget.Rendering）在 UI 线程上执行，且只在有动画组时挂接。
        /// 原实现为一条每 1ms 唤醒一次的专用线程，每帧都同步调度到 UI 线程，即使没有动画也一直空转。
        /// </summary>
        public static void AniStart()
        {
            AniLastTick = ModBase.GetTimeMs();
            AniFPSTimer = AniLastTick;
            AniRunning = true; // 标记动画执行开始
            Logger.Info("动画循环开始（跟随渲染帧）");
            EnsureAniRendering();
        }
        private static bool AniRenderingHooked = false;
        /// <summary>
        /// 确保已挂接渲染帧事件。可从任意线程调用。
        /// </summary>
        private static void EnsureAniRendering()
        {
            if (!AniRunning || AniRenderingHooked)
                return;
            var Dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (Dispatcher is null)
                return;
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(EnsureAniRendering));
                return;
            }
            if (AniRenderingHooked)
                return;
            AniLastTick = ModBase.GetTimeMs(); // 空闲后重新开始计时，避免第一帧的间隔过大导致动画瞬间完成
            CompositionTarget.Rendering += AniOnRendering;
            AniRenderingHooked = true;
        }
        private static void AniOnRendering(object sender, EventArgs e)
        {
            try
            {
                // 两帧之间的间隔时间
                long DeltaTime = (ModBase.GetTimeMs() - AniLastTick).Clamp(0L, 100000L);
                if (DeltaTime < 3L)
                    return;
                AniLastTick = ModBase.GetTimeMs();
                // 记录 FPS
                if (ModBase.ModeDebug)
                {
                    if ((AniLastTick - AniFPSTimer).Clamp(0L, 100000L) >= 500L)
                    {
                        AniFPS = AniFPSCounter;
                        AniFPSCounter = 0;
                        AniFPSTimer = AniLastTick;
                    }
                    AniFPSCounter += 2;
                }
                // 执行动画
                AniCount = 0;
                AniTimer(DeltaTime * AniSpeed);
                if (ModBase.RandomInteger(0, 64 * (ModBase.ModeDebug ? 5 : 30)) == 0 && (AniFPS < 62 && AniFPS > 0 || AniCount > 4 || ModNet.NetManager.FileRemain != 0))
                {
                    Logger.Info($"FPS {AniFPS}, 动画 {AniCount}, 下载中 {ModNet.NetManager.FileRemain}（{StringUtils.FormatByteSize(ModNet.NetManager.Speed)}/s）");
                    if (ModNet.NetManager.FileRemain < 5)
                    {
                        foreach (var File in ModNet.NetManager.AllFiles.Values)
                        {
                            if (File.State < ModNet.NetState.Finished)
                                Logger.Trace($"- {File.State} {StringUtils.FormatByteSize(File.Speed)}/s {File.LocalPath}");
                        }
                    }
                }
                // 没有动画时撤下挂接，不再占用渲染帧
                if (AniGroups.Count == 0)
                {
                    CompositionTarget.Rendering -= AniOnRendering;
                    AniRenderingHooked = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "动画帧执行失败", LogBehavior.AlertThenCrash);
            }
        }

        /// <summary>
    /// 动画定时器事件。
    /// </summary>
        public static void AniTimer(double DeltaTick)
        {
            try
            {
                if (DeltaTick / AniSpeed > 100d)
                    Logger.Info($"两个动画帧间隔 {DeltaTick} ms");
                int i = -1;
                // 循环每个动画组
                while (i + 1 < AniGroups.Count)
                {
                    i += 1;
                    // 初始化
                    var Entry = AniGroups.Values.ElementAtOrDefault(i);
                    if (Entry.StartTick > AniLastTick)
                        continue; // 跳过本刻之后开始的动画
                    bool CanRemoveAfter = true; // 是否应该去除“之后”标记
                    int ii = 0;

                    // 循环每个动画
                    while (ii < Entry.Data.Count)
                    {
                        var Anim = Entry.Data[ii];
                        // 执行种类
                        if (Anim.IsAfter == false) // 之前
                        {
                            CanRemoveAfter = false; // 取消“之后”标记 
                                                    // 增加执行时间
                            Anim.TimeFinished += DeltaTick;
                            // 执行动画
                            if (Anim.TimeFinished > 0d)
                            {
                                Anim = AniRun(Anim);
                                AniCount += 1;
                            }
                            // 如果当前动画已执行完毕
                            if (Anim.TimeFinished >= Anim.TimeTotal)
                            {
                                // 如果是去向颜色资源的动画，设置引用
                                if (Anim.TypeMain == AniType.Color && Anim.Obj is object[] ColorPair && !string.IsNullOrEmpty(ColorPair[2] as string))
                                {
                                    if (ColorPair[0] is FrameworkElement ColorTarget)
                                        ColorTarget.SetResourceReference((DependencyProperty)ColorPair[1], ColorPair[2]);
                                    else
                                        ((FrameworkContentElement)ColorPair[0]).SetResourceReference((DependencyProperty)ColorPair[1], ColorPair[2]);
                                }
                                // 删除：下一个动画移到了当前位置，下标不变
                                Entry.Data.RemoveAt(ii);
                                continue;
                            }
                            Entry.Data[ii] = Anim;
                        }
                        else if (CanRemoveAfter) // 之后
                        {
                            // 之后改为之前，并重新处理该动画（下标不变）
                            CanRemoveAfter = false;
                            Anim.IsAfter = false;
                            Entry.Data[ii] = Anim;
                            continue;
                        }
                        else
                        {
                            // 不能去除该“之后”标记，结束该动画组
                            break;
                        }
                        ii += 1;
                    }

                    // 如果当前动画组都执行完毕则删除
                    if (!Entry.Data.Any())
                    {
                        // 为了避免新添加的动画影响顺序，不能 RemoveAt(i)
                        // 为了允许动画在执行中添加同名动画组，不能按名字移除
                        for (int Current = 0, loopTo = AniGroups.Count - 1; Current <= loopTo; Current++)
                        {
                            if (AniGroups.ElementAt(Current).Value.Uuid == Entry.Uuid)
                            {
                                AniGroups.Remove(AniGroups.ElementAt(Current).Key);
                                break;
                            }
                        }
                        i -= 1;
                    }
                }
            }

            catch (Exception ex)
            {
                Logger.Error(ex, "动画刻执行失败", LogBehavior.Toast);
            }
        }

        /// <summary>
    /// 执行一个动画。
    /// </summary>
    /// <param name="Ani">执行的动画对象。</param>
        private static AniData AniRun(AniData Ani)
        {
            try
            {
                switch (Ani.TypeMain)
                {

                    case AniType.Number:
                        {
                            double Delta = MathUtils.Lerp(0d, Coerce.ToDouble(Ani.Value), Ani.Ease.GetDelta(Ani.TimeFinished / Ani.TimeTotal, Ani.TimePercent));
                            if (Delta != 0d)
                            {
                                switch (Ani.TypeSub)
                                {
                                    case AniTypeSub.X:
                                        {
                                            if (Ani.Obj is Window)
                                            {
                                                ((Window)Ani.Obj).Left += Delta;
                                            }
                                            else
                                            {
                                                var Element = (FrameworkElement)Ani.Obj;
                                                var Margin = Element.Margin;
                                                switch (Element.HorizontalAlignment)
                                                {
                                                    case HorizontalAlignment.Left:
                                                    case HorizontalAlignment.Stretch:
                                                        Element.Margin = new Thickness(Margin.Left + Delta, Margin.Top, Margin.Right, Margin.Bottom);
                                                        break;
                                                    case HorizontalAlignment.Right:
                                                        Element.Margin = new Thickness(Margin.Left, Margin.Top, Margin.Right - Delta, Margin.Bottom);
                                                        break;
                                                }
                                            }

                                            break;
                                        }
                                    case AniTypeSub.Y:
                                        {
                                            if (Ani.Obj is Window)
                                            {
                                                ((Window)Ani.Obj).Top += Delta;
                                            }
                                            else
                                            {
                                                var Element = (FrameworkElement)Ani.Obj;
                                                var Margin = Element.Margin;
                                                switch (Element.VerticalAlignment)
                                                {
                                                    case VerticalAlignment.Top:
                                                        Element.Margin = new Thickness(Margin.Left, Margin.Top + Delta, Margin.Right, Margin.Bottom);
                                                        break;
                                                    case VerticalAlignment.Bottom:
                                                        Element.Margin = new Thickness(Margin.Left, Margin.Top, Margin.Right, Margin.Bottom - Delta);
                                                        break;
                                                }
                                            }

                                            break;
                                        }
                                    case AniTypeSub.Opacity:
                                        {
                                            if (Ani.Obj is UIElement)
                                            {
                                                UIElement Obj = (UIElement)Ani.Obj;
                                                Obj.Opacity = (Obj.Opacity + Delta).Clamp(0d, 1d);
                                            }
                                            else if (Ani.Obj is DropShadowEffect)
                                            {
                                                DropShadowEffect Obj = (DropShadowEffect)Ani.Obj;
                                                Obj.Opacity = (Obj.Opacity + Delta).Clamp(0d, 1d);
                                            }

                                            break;
                                        }
                                    case AniTypeSub.Width:
                                        {
                                            FrameworkElement Obj = (FrameworkElement)Ani.Obj;
                                            Obj.Width = Math.Max((double.IsNaN(Obj.Width) ? Obj.ActualWidth : Obj.Width) + Delta, 0d);
                                            break;
                                        }
                                    case AniTypeSub.Height:
                                        {
                                            FrameworkElement Obj = (FrameworkElement)Ani.Obj;
                                            Obj.Height = Math.Max((double.IsNaN(Obj.Height) ? Obj.ActualHeight : Obj.Height) + Delta, 0d);
                                            break;
                                        }
                                    case AniTypeSub.Value:
                                        {
                                            ((System.Windows.Controls.Primitives.RangeBase)Ani.Obj).Value += Delta;
                                            break;
                                        }
                                    case AniTypeSub.Radius:
                                        {
                                            ((System.Windows.Media.Effects.BlurEffect)Ani.Obj).Radius += Delta;
                                            break;
                                        }
                                    case AniTypeSub.StrokeThickness:
                                        {
                                            var Shape = (System.Windows.Shapes.Shape)Ani.Obj;
                                            Shape.StrokeThickness = Math.Max(Shape.StrokeThickness + Delta, 0d);
                                            break;
                                        }
                                    case AniTypeSub.BorderThickness:
                                        {
                                            if (Ani.Obj is Border BorderObj)
                                                BorderObj.BorderThickness = new Thickness(BorderObj.BorderThickness.Bottom + Delta);
                                            else
                                                ((Control)Ani.Obj).BorderThickness = new Thickness(((Control)Ani.Obj).BorderThickness.Bottom + Delta);
                                            break;
                                        }
                                    case AniTypeSub.TranslateX:
                                        {
                                            var Element = (UIElement)Ani.Obj;
                                            if (Element.RenderTransform is not TranslateTransform)
                                                Element.RenderTransform = new TranslateTransform(0d, 0d);
                                            ((TranslateTransform)Element.RenderTransform).X += Delta;
                                            break;
                                        }
                                    case AniTypeSub.TranslateY:
                                        {
                                            var Element = (UIElement)Ani.Obj;
                                            if (Element.RenderTransform is not TranslateTransform)
                                                Element.RenderTransform = new TranslateTransform(0d, 0d);
                                            ((TranslateTransform)Element.RenderTransform).Y += Delta;
                                            break;
                                        }
                                    case AniTypeSub.Double:
                                        {
                                            var Pair = (object[])Ani.Obj;
                                            var Target = (DependencyObject)Pair[0];
                                            var Property = (DependencyProperty)Pair[1];
                                            Target.SetValue(Property, Coerce.ToDouble(Target.GetValue(Property)) + Delta);
                                            break;
                                        }
                                    case AniTypeSub.DoubleParam:
                                        {
                                            ((ParameterizedThreadStart)Ani.Obj)(Delta);
                                            break;
                                        }
                                    case AniTypeSub.GridLengthWidth:
                                        {
                                            var Column = (ColumnDefinition)Ani.Obj;
                                            Column.Width = new GridLength(Math.Max(Column.Width.Value + Delta, 0d), GridUnitType.Star);
                                            break;
                                        }
                                }
                            }

                            break;
                        }

                    case AniType.Color:
                        {
                            // 利用 Last 记录了余下的小数值
                            var Delta = ModBase.MyColor.Lerp(new ModBase.MyColor(0d, 0d, 0d, 0d), (ModBase.MyColor)Ani.Value, Ani.Ease.GetDelta(Ani.TimeFinished / Ani.TimeTotal, Ani.TimePercent)) + (ModBase.MyColor)Ani.ValueLast;
                            FrameworkElement Obj = (FrameworkElement)((object[])Ani.Obj)[0];
                            DependencyProperty Prop = (DependencyProperty)((object[])Ani.Obj)[1];
                            var NewColor = new ModBase.MyColor(Obj.GetValue(Prop)) + Delta;
                            Obj.SetValue(Prop, Prop.PropertyType.Name == "Color" ? (global::System.Windows.Media.Color)NewColor : (global::System.Windows.Media.SolidColorBrush)NewColor);
                            Ani.ValueLast = NewColor - new ModBase.MyColor(Obj.GetValue(Prop));
                            break;
                        }

                    case AniType.Scale:
                        {
                            FrameworkElement Obj = (FrameworkElement)Ani.Obj;
                            double Delta = Ani.Ease.GetDelta(Ani.TimeFinished / Ani.TimeTotal, Ani.TimePercent);
                            var Rect = (ModBase.MyRect)Ani.Value;
                            Obj.Margin = new Thickness(Obj.Margin.Left + MathUtils.Lerp(0d, Rect.Left, Delta), Obj.Margin.Top + MathUtils.Lerp(0d, Rect.Top, Delta), Obj.Margin.Right + MathUtils.Lerp(0d, Rect.Left, Delta), Obj.Margin.Bottom + MathUtils.Lerp(0d, Rect.Top, Delta));
                            Obj.Width = Math.Max(Obj.Width + MathUtils.Lerp(0d, Rect.Width, Delta), 0d);
                            Obj.Height = Math.Max(Obj.Height + MathUtils.Lerp(0d, Rect.Height, Delta), 0d);
                            break;
                        }

                    case AniType.TextAppear:
                        {
                            string Text = Coerce.ToStr(Ani.Value.Args()[0]);
                            int TextCount = (int)Math.Round((double)(Coerce.ToBool(Ani.Value.Args()[1]) ? Text.Length : 0) + Math.Round((double)(Text.Length * (Coerce.ToBool(Ani.Value.Args()[1]) ? -1 : 1)) * Ani.Ease.GetDelta(Ani.TimeFinished / Ani.TimeTotal, 0d)));
                            string NewText = Text.Substring(0, TextCount);
                            // 添加乱码
                            if (TextCount < Text.Length)
                            {
                                string NextText = Text.Substring(TextCount, 1);
                                if (Convert.ToInt32(Convert.ToChar(NextText)) >= Convert.ToInt32(Convert.ToChar(128)))
                                {
                                    NewText += Encoding.GetEncoding("GB18030").GetString(new[] { (byte)ModBase.RandomInteger(16 + 160, 87 + 160), (byte)ModBase.RandomInteger(1 + 160, 89 + 160) });
                                }
                                else
                                {
                                    NewText += ModBase.RandomInteger(0, 85).ConvertRadix(86);
                                }
                            }
                            // 设置文本
                            if (Ani.Obj is TextBlock)
                            {
                                ((TextBlock)Ani.Obj).Text = NewText;
                            }
                            else
                            {
                                ((ContentControl)Ani.Obj).Content = NewText;
                            }

                            break;
                        }

                    case AniType.Code:
                        {
                            Ani.TimeFinished = Ani.TimeTotal;
                            ((ThreadStart)Ani.Value)();
                            break;
                        }

                    case AniType.ScaleTransform:
                        {
                            FrameworkElement Obj = (FrameworkElement)Ani.Obj;
                            if (!(Obj.RenderTransform is ScaleTransform))
                            {
                                if (Obj.RenderTransformOrigin == new Point(0d, 0d))
                                    Obj.RenderTransformOrigin = new Point(0.5d, 0.5d);
                                Obj.RenderTransform = new ScaleTransform(1d, 1d);
                            }
                            double Delta = MathUtils.Lerp(0d, Coerce.ToDouble(Ani.Value), Ani.Ease.GetDelta(Ani.TimeFinished / Ani.TimeTotal, Ani.TimePercent));
                            ((ScaleTransform)Obj.RenderTransform).ScaleX = Math.Max(((ScaleTransform)Obj.RenderTransform).ScaleX + Delta, 0d);
                            ((ScaleTransform)Obj.RenderTransform).ScaleY = Math.Max(((ScaleTransform)Obj.RenderTransform).ScaleY + Delta, 0d);
                            break;
                        }

                    case AniType.RotateTransform:
                        {
                            FrameworkElement Obj = (FrameworkElement)Ani.Obj;
                            if (!(Obj.RenderTransform is RotateTransform))
                            {
                                if (Obj.RenderTransformOrigin == new Point(0d, 0d))
                                    Obj.RenderTransformOrigin = new Point(0.5d, 0.5d);
                                Obj.RenderTransform = new RotateTransform(0d);
                            }
                            double Delta = MathUtils.Lerp(0d, Coerce.ToDouble(Ani.Value), Ani.Ease.GetDelta(Ani.TimeFinished / Ani.TimeTotal, Ani.TimePercent));
                            ((RotateTransform)Obj.RenderTransform).Angle = ((RotateTransform)Obj.RenderTransform).Angle + Delta;
                            break;
                        }

                }
                Ani.TimePercent = Ani.TimeFinished / Ani.TimeTotal; // 修改执行百分比
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"执行动画失败：{Ani}", LogBehavior.Toast);
            }
            return Ani;
        }

    }
}